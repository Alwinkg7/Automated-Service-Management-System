// =================================================================
//  PaymentService.cs — ServiceApp.Services/Implementations
//
//  Implements IPaymentService.
//
//  WHAT MAKES THIS CRITICAL:
//  The completion flow touches 4 tables in one transaction:
//    1. Payments (INSERT)
//    2. Bills (UPDATE PaymentStatus + PaidAt)
//    3. ServiceRequests (UPDATE Status → Completed)
//    4. TechnicianProfiles (UPDATE Status + TotalJobsCompleted)
//    5. ServiceHistories (INSERT)
//
//  If ANY step fails, ALL are rolled back.
//  This is why UnitOfWork transactions exist.
//
//  IDEMPOTENCY:
//  We check if a Payment already exists for this bill before
//  creating one. If it does → return the existing payment.
//  This prevents double-charging if the user clicks Pay twice.
// =================================================================

using Microsoft.Extensions.Logging;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Services.Implementations
{
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<PaymentService> _logger;
        private readonly IRazorpayService _razorpay;
        private readonly INotificationService _notifications;
        private readonly IEmailService _email;

        public PaymentService(IUnitOfWork uow, ILogger<PaymentService> logger, IRazorpayService razorpay, INotificationService notifications, IEmailService email)
        {
            _uow = uow;
            _logger = logger;
            _razorpay = razorpay;
            _notifications = notifications;
            _email = email;
        }

        // =============================================================
        //  GET BILL FOR PAYMENT PAGE
        //  Validates ownership and status before showing Pay page.
        // =============================================================
        public async Task<Result<Bill>> GetBillForPaymentAsync(
            int requestId,
            string customerUserId)
        {
            // Load the bill with all details
            var bill = await _uow.Bills.GetByRequestIdAsync(requestId);
            if (bill == null)
                return Result<Bill>.Failure(
                    "No bill found for this request.");

            // Load the request to verify ownership
            var request = await _uow.ServiceRequests
                .GetByIdAsync(requestId);
            if (request == null)
                return Result<Bill>.Failure("Request not found.");

            // Security: only the customer who owns the request can pay
            if (request.CustomerId != customerUserId)
                return Result<Bill>.Failure(
                    "You can only pay bills for your own requests.");

            // Must be in Billed status to pay
            if (request.Status != RequestStatus.Billed)
                return Result<Bill>.Failure(
                    $"This request is {request.Status} " +
                    "and does not have a pending payment.");

            // Already paid? Return success with existing bill
            if (bill.PaymentStatus == PaymentStatus.Paid)
                return Result<Bill>.Failure(
                    "This bill has already been paid.");

            return Result<Bill>.Success(bill);
        }

        // =============================================================
        //  PROCESS CASH PAYMENT
        //
        //  Atomic transaction — all 5 steps succeed or all fail:
        //  1. Create Payment record
        //  2. Update Bill → Paid
        //  3. Update Request → Completed
        //  4. Update Technician → Available + increment job count
        //  5. Log ServiceHistory
        // =============================================================
        public async Task<Result<Payment>> ProcessCashPaymentAsync(
            int billId,
            string customerUserId)
        {
            // Load bill with full details
            var bill = await _uow.Bills.GetWithItemsAndPaymentAsync(billId);
            if (bill == null)
                return Result<Payment>.Failure("Bill not found.");

            // ── Idempotency check ──────────────────────────────────
            // If payment already exists for this bill → skip
            // Prevents double-payment if user submits form twice
            var existingPayment = await _uow.Payments
                .GetByBillIdAsync(billId);
            if (existingPayment != null)
                return Result<Payment>.Success(existingPayment);

            // Load the request
            var request = await _uow.ServiceRequests
                .GetByIdAsync(bill.ServiceRequestId);
            if (request == null)
                return Result<Payment>.Failure("Request not found.");

            // Verify ownership
            if (request.CustomerId != customerUserId)
                return Result<Payment>.Failure(
                    "You can only pay your own bills.");

            // Verify status
            if (request.Status != RequestStatus.Billed)
                return Result<Payment>.Failure(
                    $"Cannot process payment — " +
                    $"request status is {request.Status}.");

            await _uow.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;

                // ── Step 1: Create Payment record ──────────────────
                var payment = new Payment
                {
                    BillId = billId,
                    Amount = bill.TotalAmount,
                    PaymentMethod = "Cash",
                    GatewayTransactionId = $"CASH-{billId}-{now:yyyyMMddHHmmss}",
                    GatewayOrderId = null,
                    PaidAt = now
                };
                await _uow.Payments.AddAsync(payment);

                // Save to get PaymentId
                await _uow.SaveChangesAsync();

                // ── Step 2: Update Bill → Paid ──────────────────────
                bill.PaymentStatus = PaymentStatus.Paid;
                bill.PaidAt = now;
                _uow.Bills.Update(bill);

                // ── Step 3: Update Request → Completed ─────────────
                request.Status = RequestStatus.Completed;
                request.UpdatedAt = now;
                _uow.ServiceRequests.Update(request);

                // ── Step 4: Free the technician ─────────────────────
                if (request.AssignedTechnicianProfileId.HasValue)
                {
                    var tech = await _uow.TechnicianProfiles
                        .GetByIdAsync(
                            request.AssignedTechnicianProfileId.Value);

                    if (tech != null)
                    {
                        // Back to Available — can receive new jobs
                        tech.Status = TechnicianStatus.Available;

                        // Increment lifetime jobs counter
                        tech.TotalJobsCompleted += 1;

                        _uow.TechnicianProfiles.Update(tech);
                    }
                }

                // ── Step 5: Log ServiceHistory ──────────────────────
                var history = new ServiceHistory
                {
                    RequestId = bill.ServiceRequestId,
                    Status = RequestStatus.Completed,
                    ChangedByUserId = customerUserId,
                    Note = $"Payment of ₹{bill.TotalAmount:N2} " +
                                      $"received via Cash. " +
                                      "Job completed successfully.",
                    ChangedAt = now
                };
                await _uow.ServiceHistories.AddAsync(history);

                // ── Commit all 5 steps together ─────────────────────
                await _uow.CommitTransactionAsync();

                // Load customer and technician info for emails
                var customerUser = await _uow.Users.GetByIdAsync(request.CustomerId);

                // Email customer receipt
                if (customerUser != null)
                {
                    _ = _email.SendPaymentReceiptToCustomerAsync(
                        customerUser.Email!,
                        customerUser.FullName,
                        bill.ServiceRequestId,
                        bill.Id,
                        bill.TotalAmount,
                        payment.PaymentMethod,
                        payment.GatewayTransactionId ?? "N/A",
                        payment.PaidAt);
                }

                // Email technician
                if (request.AssignedTechnicianProfileId.HasValue)
                {
                    var tech = await _uow.TechnicianProfiles
                        .GetWithUserAsync(
                            request.AssignedTechnicianProfileId.Value);

                    if (tech?.User != null)
                    {
                        _ = _email.SendPaymentReceiptToTechnicianAsync(
                            tech.User.Email!,
                            tech.User.FullName,
                            bill.ServiceRequestId,
                            bill.TotalAmount);
                    }
                }

                // Notify technician — job done, they're available again
                if (request.AssignedTechnicianProfileId.HasValue)
                {
                    var tech = await _uow.TechnicianProfiles
                        .GetByIdAsync(request.AssignedTechnicianProfileId.Value);
                    if (tech != null)
                    {
                        await _notifications.NotifyTechnicianJobCompletedAsync(
                            tech.UserId,
                            bill.ServiceRequestId,
                            bill.TotalAmount);
                    }
                }

                await _notifications.NotifyAdminStatusChangedAsync(
                    bill.ServiceRequestId, "Completed");

                _logger.LogInformation(
                    "Payment processed for bill #{BillId}. " +
                    "Request #{RequestId} completed. " +
                    "Amount: ₹{Amount}",
                    billId, bill.ServiceRequestId, bill.TotalAmount);

                return Result<Payment>.Success(payment);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Payment failed for bill #{BillId}", billId);
                return Result<Payment>.Failure(
                    "Payment processing failed. Please try again.");
            }
        }
        public async Task<Payment?> GetPaymentByIdAsync(int paymentId)
        {
            var payment = await _uow.Payments.GetByIdAsync(paymentId);
            return payment;
        }

        // =============================================================
        //  CREATE RAZORPAY ORDER
        //  Server-side order creation — required before showing
        //  Razorpay checkout JS to the customer.
        // =============================================================
        public async Task<Result<RazorpayOrderResult>> CreateRazorpayOrderAsync(
            int billId,
            string customerUserId)
        {
            // Load and validate the bill
            var bill = await _uow.Bills.GetWithItemsAndPaymentAsync(billId);
            if (bill == null)
                return Result<RazorpayOrderResult>.Failure("Bill not found.");

            var request = await _uow.ServiceRequests
                .GetByIdAsync(bill.ServiceRequestId);
            if (request == null)
                return Result<RazorpayOrderResult>.Failure("Request not found.");

            if (request.CustomerId != customerUserId)
                return Result<RazorpayOrderResult>.Failure(
                    "You can only pay your own bills.");

            if (request.Status != RequestStatus.Billed)
                return Result<RazorpayOrderResult>.Failure(
                    "This request is not ready for payment.");

            if (bill.PaymentStatus == PaymentStatus.Paid)
                return Result<RazorpayOrderResult>.Failure(
                    "This bill has already been paid.");

            try
            {
                // Create order on Razorpay — get an order ID
                var receiptId = $"bill_{billId}_{DateTime.UtcNow:yyyyMMdd}";

                var order = await _razorpay.CreateOrderAsync(
                    amount: bill.TotalAmount,
                    currency: "INR",
                    receiptId: receiptId);

                _logger.LogInformation(
                    "Razorpay order {OrderId} created for bill #{BillId}",
                    order.OrderId, billId);

                return Result<RazorpayOrderResult>.Success(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to create Razorpay order for bill #{BillId}", billId);
                return Result<RazorpayOrderResult>.Failure(
                    "Could not initiate payment. Please try again.");
            }
        }

        // =============================================================
        //  PROCESS RAZORPAY PAYMENT
        //  Called after customer completes Razorpay checkout.
        //  Razorpay JS sends: orderId, paymentId, signature.
        //
        //  FLOW:
        //  1. Verify signature (reject if invalid)
        //  2. Idempotency check (skip if already processed)
        //  3. Run the same 5-step completion transaction as cash
        // =============================================================
        public async Task<Result<Payment>> ProcessRazorpayPaymentAsync(
            int billId,
            string customerUserId,
            string razorpayOrderId,
            string razorpayPaymentId,
            string razorpaySignature)
        {
            // ── Step 1: Verify signature ───────────────────────────────
            // This is the CRITICAL security check.
            // If signature is wrong → someone tampered with the payment.
            var isValid = _razorpay.VerifyPaymentSignature(
                razorpayOrderId,
                razorpayPaymentId,
                razorpaySignature);

            if (!isValid)
            {
                _logger.LogWarning(
                    "Invalid Razorpay signature for bill #{BillId}. " +
                    "Possible fraud attempt. OrderId: {OrderId}",
                    billId, razorpayOrderId);
                return Result<Payment>.Failure(
                    "Payment verification failed. " +
                    "Please contact support if money was deducted.");
            }

            // ── Step 2: Idempotency check ──────────────────────────────
            var existingPayment = await _uow.Payments
                .GetByGatewayTransactionIdAsync(razorpayPaymentId);
            if (existingPayment != null)
            {
                _logger.LogInformation(
                    "Duplicate payment attempt for {PaymentId} — skipped",
                    razorpayPaymentId);
                return Result<Payment>.Success(existingPayment);
            }

            // ── Step 3: Load bill and validate ────────────────────────
            var bill = await _uow.Bills.GetWithItemsAndPaymentAsync(billId);
            if (bill == null)
                return Result<Payment>.Failure("Bill not found.");

            var request = await _uow.ServiceRequests
                .GetByIdAsync(bill.ServiceRequestId);
            if (request == null)
                return Result<Payment>.Failure("Request not found.");

            if (request.CustomerId != customerUserId)
                return Result<Payment>.Failure(
                    "You can only pay your own bills.");

            // ── Step 4: Run the completion transaction ─────────────────
            await _uow.BeginTransactionAsync();
            try
            {
                var now = DateTime.UtcNow;

                // Insert Payment row
                var payment = new Payment
                {
                    BillId = billId,
                    Amount = bill.TotalAmount,
                    PaymentMethod = "Razorpay",
                    GatewayTransactionId = razorpayPaymentId,
                    GatewayOrderId = razorpayOrderId,
                    PaidAt = now
                };
                await _uow.Payments.AddAsync(payment);
                await _uow.SaveChangesAsync();

                // Bill → Paid
                bill.PaymentStatus = PaymentStatus.Paid;
                bill.PaidAt = now;
                _uow.Bills.Update(bill);

                // Request → Completed
                request.Status = RequestStatus.Completed;
                request.UpdatedAt = now;
                _uow.ServiceRequests.Update(request);

                // Technician → Available + increment job count
                if (request.AssignedTechnicianProfileId.HasValue)
                {
                    var tech = await _uow.TechnicianProfiles
                        .GetByIdAsync(
                            request.AssignedTechnicianProfileId.Value);
                    if (tech != null)
                    {
                        tech.Status = TechnicianStatus.Available;
                        tech.TotalJobsCompleted += 1;
                        _uow.TechnicianProfiles.Update(tech);
                    }
                }

                // Log ServiceHistory
                var history = new ServiceHistory
                {
                    RequestId = bill.ServiceRequestId,
                    Status = RequestStatus.Completed,
                    ChangedByUserId = customerUserId,
                    Note = $"Payment of ₹{bill.TotalAmount:N2} " +
                                      $"received via Razorpay. " +
                                      $"Transaction: {razorpayPaymentId}.",
                    ChangedAt = now
                };
                await _uow.ServiceHistories.AddAsync(history);

                await _uow.CommitTransactionAsync();

                // Load customer and technician info for emails
                var customerUser = await _uow.Users.GetByIdAsync(request.CustomerId);

                // Email customer receipt
                if (customerUser != null)
                {
                    _ = _email.SendPaymentReceiptToCustomerAsync(
                        customerUser.Email!,
                        customerUser.FullName,
                        bill.ServiceRequestId,
                        bill.Id,
                        bill.TotalAmount,
                        payment.PaymentMethod,
                        payment.GatewayTransactionId ?? "N/A",
                        payment.PaidAt);
                }

                // Email technician
                if (request.AssignedTechnicianProfileId.HasValue)
                {
                    var tech = await _uow.TechnicianProfiles
                        .GetWithUserAsync(
                            request.AssignedTechnicianProfileId.Value);

                    if (tech?.User != null)
                    {
                        _ = _email.SendPaymentReceiptToTechnicianAsync(
                            tech.User.Email!,
                            tech.User.FullName,
                            bill.ServiceRequestId,
                            bill.TotalAmount);
                    }
                }

                // Notify technician — job done, they're available again
                if (request.AssignedTechnicianProfileId.HasValue)
                {
                    var tech = await _uow.TechnicianProfiles
                        .GetByIdAsync(request.AssignedTechnicianProfileId.Value);
                    if (tech != null)
                    {
                        await _notifications.NotifyTechnicianJobCompletedAsync(
                            tech.UserId,
                            bill.ServiceRequestId,
                            bill.TotalAmount);
                    }
                }

                await _notifications.NotifyAdminStatusChangedAsync(
                    bill.ServiceRequestId, "Completed");

                _logger.LogInformation(
                    "Razorpay payment {PaymentId} processed. " +
                    "Bill #{BillId} paid. Request #{RequestId} completed.",
                    razorpayPaymentId, billId, bill.ServiceRequestId);

                return Result<Payment>.Success(payment);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Failed to process Razorpay payment for bill #{BillId}",
                    billId);
                return Result<Payment>.Failure(
                    "Payment was received but processing failed. " +
                    "Please contact support with transaction ID: " +
                    razorpayPaymentId);
            }
        }
    }
}