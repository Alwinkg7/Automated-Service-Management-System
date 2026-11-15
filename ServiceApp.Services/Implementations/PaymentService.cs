using Microsoft.Extensions.Logging;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Services.Implementations
{
    /// <summary>
    /// Payment workflows. Every successful payment runs the same completion
    /// transaction (Payment insert, Bill paid, Request completed, Technician freed,
    /// ServiceHistory insert); side effects (loyalty points, e-mails, SignalR) run
    /// only after the commit and never turn a committed payment into a failure.
    /// </summary>
    public class PaymentService : IPaymentService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<PaymentService> _logger;
        private readonly IRazorpayService _razorpay;
        private readonly INotificationService _notifications;
        private readonly IEmailService _email;
        private readonly IPromotionService _promotions;

        public PaymentService(
            IUnitOfWork uow,
            ILogger<PaymentService> logger,
            IRazorpayService razorpay,
            INotificationService notifications,
            IEmailService email,
            IPromotionService promotions)
        {
            _uow = uow;
            _logger = logger;
            _razorpay = razorpay;
            _notifications = notifications;
            _email = email;
            _promotions = promotions;
        }

        // ── Queries ───────────────────────────────────────────────────

        public async Task<Result<Bill>> GetBillForPaymentAsync(int requestId, string customerUserId)
        {
            var bill = await _uow.Bills.GetByRequestIdAsync(requestId);
            if (bill == null)
                return Result<Bill>.Failure("No bill found for this request.", ErrorType.NotFound);

            var request = await _uow.ServiceRequests.GetByIdAsync(requestId);
            if (request == null)
                return Result<Bill>.Failure("Request not found.", ErrorType.NotFound);

            if (request.CustomerId != customerUserId)
                return Result<Bill>.Failure("You can only pay bills for your own requests.", ErrorType.Forbidden);

            if (bill.PaymentStatus == PaymentStatus.Paid)
                return Result<Bill>.Failure("This bill has already been paid.", ErrorType.Conflict);

            if (request.Status != RequestStatus.Billed)
                return Result<Bill>.Failure(
                    $"This request is {request.Status} and does not have a pending payment.",
                    ErrorType.Conflict);

            return Result<Bill>.Success(bill);
        }

        public Task<Payment?> GetPaymentByIdAsync(int paymentId) =>
            _uow.Payments.GetByIdAsync(paymentId);

        // ── Cash ──────────────────────────────────────────────────────

        public async Task<Result<Payment>> ProcessCashPaymentAsync(int billId, string customerUserId)
        {
            var bill = await _uow.Bills.GetWithItemsAndPaymentAsync(billId);
            if (bill == null)
                return Result<Payment>.Failure("Bill not found.", ErrorType.NotFound);

            // Idempotency: a second click returns the payment already recorded.
            var existingPayment = await _uow.Payments.GetByBillIdAsync(billId);
            if (existingPayment != null)
                return Result<Payment>.Success(existingPayment);

            var request = await _uow.ServiceRequests.GetByIdAsync(bill.ServiceRequestId);
            if (request == null)
                return Result<Payment>.Failure("Request not found.", ErrorType.NotFound);

            if (request.CustomerId != customerUserId)
                return Result<Payment>.Failure("You can only pay your own bills.", ErrorType.Forbidden);

            if (request.Status != RequestStatus.Billed)
                return Result<Payment>.Failure(
                    $"Cannot process payment — request status is {request.Status}.", ErrorType.Conflict);

            var now = DateTime.UtcNow;
            return await CompletePaymentAsync(
                bill,
                request,
                method: "Cash",
                transactionId: $"CASH-{billId}-{now:yyyyMMddHHmmss}",
                orderId: null,
                changedByUserId: customerUserId,
                historyNote: $"Payment of ₹{bill.TotalAmount:N2} received via Cash. Job completed successfully.",
                failureMessage: "Payment processing failed. Please try again.");
        }

        public async Task<Result<Payment>> ConfirmCashCollectedAsync(int requestId, string technicianUserId)
        {
            var tech = await _uow.TechnicianProfiles.GetByUserIdAsync(technicianUserId);
            if (tech == null)
                return Result<Payment>.Failure("Technician profile not found.", ErrorType.NotFound);

            var request = await _uow.ServiceRequests.GetByIdAsync(requestId);
            if (request == null)
                return Result<Payment>.Failure("Request not found.", ErrorType.NotFound);

            if (request.AssignedTechnicianProfileId != tech.TechnicianProfileId)
                return Result<Payment>.Failure("This job is not assigned to you.", ErrorType.Forbidden);

            var bill = await _uow.Bills.GetByRequestIdAsync(requestId);
            if (bill == null)
                return Result<Payment>.Failure("No bill exists for this job yet.", ErrorType.Conflict);

            var existingPayment = await _uow.Payments.GetByBillIdAsync(bill.Id);
            if (existingPayment != null || bill.PaymentStatus == PaymentStatus.Paid)
                return Result<Payment>.Failure("This bill has already been paid.", ErrorType.Conflict);

            if (request.Status != RequestStatus.Billed)
                return Result<Payment>.Failure(
                    $"Only Billed jobs can be completed (current status: {request.Status}).", ErrorType.Conflict);

            var now = DateTime.UtcNow;
            return await CompletePaymentAsync(
                bill,
                request,
                method: "Cash",
                transactionId: $"CASH-{bill.Id}-{now:yyyyMMddHHmmss}",
                orderId: null,
                changedByUserId: technicianUserId,
                historyNote: $"Cash payment of ₹{bill.TotalAmount:N2} confirmed by technician. Job completed.",
                failureMessage: "Could not record the cash payment. Please try again.");
        }

        // ── Razorpay checkout ─────────────────────────────────────────

        public async Task<Result<RazorpayOrderResult>> CreateRazorpayOrderAsync(int billId, string customerUserId)
        {
            var bill = await _uow.Bills.GetWithItemsAndPaymentAsync(billId);
            if (bill == null)
                return Result<RazorpayOrderResult>.Failure("Bill not found.", ErrorType.NotFound);

            var request = await _uow.ServiceRequests.GetByIdAsync(bill.ServiceRequestId);
            if (request == null)
                return Result<RazorpayOrderResult>.Failure("Request not found.", ErrorType.NotFound);

            if (request.CustomerId != customerUserId)
                return Result<RazorpayOrderResult>.Failure("You can only pay your own bills.", ErrorType.Forbidden);

            if (bill.PaymentStatus == PaymentStatus.Paid)
                return Result<RazorpayOrderResult>.Failure("This bill has already been paid.", ErrorType.Conflict);

            if (request.Status != RequestStatus.Billed)
                return Result<RazorpayOrderResult>.Failure("This request is not ready for payment.", ErrorType.Conflict);

            try
            {
                var order = await _razorpay.CreateOrderAsync(
                    amount: bill.TotalAmount,
                    currency: "INR",
                    receiptId: $"bill_{billId}_{DateTime.UtcNow:yyyyMMdd}",
                    billId: billId);

                // Bind the order to the bill: checkout/webhook payments must carry this order id.
                bill.RazorpayOrderId = order.OrderId;
                _uow.Bills.Update(bill);
                await _uow.SaveChangesAsync();

                _logger.LogInformation("Razorpay order {OrderId} created for bill #{BillId}", order.OrderId, billId);
                return Result<RazorpayOrderResult>.Success(order);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create Razorpay order for bill #{BillId}", billId);
                return Result<RazorpayOrderResult>.Failure(
                    "Could not initiate payment. Please try again.", ErrorType.Dependency);
            }
        }

        public async Task<Result<Payment>> ProcessRazorpayPaymentAsync(
            int billId,
            string customerUserId,
            string razorpayOrderId,
            string razorpayPaymentId,
            string razorpaySignature)
        {
            if (!_razorpay.VerifyPaymentSignature(razorpayOrderId, razorpayPaymentId, razorpaySignature))
            {
                _logger.LogWarning(
                    "Invalid Razorpay signature for bill #{BillId}. OrderId: {OrderId}",
                    billId, razorpayOrderId);
                return Result<Payment>.Failure(
                    "Payment verification failed. Please contact support if money was deducted.",
                    ErrorType.Validation);
            }

            var existingPayment = await _uow.Payments.GetByGatewayTransactionIdAsync(razorpayPaymentId);
            if (existingPayment != null)
            {
                _logger.LogInformation("Duplicate payment attempt for {PaymentId} — skipped", razorpayPaymentId);
                return Result<Payment>.Success(existingPayment);
            }

            var bill = await _uow.Bills.GetWithItemsAndPaymentAsync(billId);
            if (bill == null)
                return Result<Payment>.Failure("Bill not found.", ErrorType.NotFound);

            var request = await _uow.ServiceRequests.GetByIdAsync(bill.ServiceRequestId);
            if (request == null)
                return Result<Payment>.Failure("Request not found.", ErrorType.NotFound);

            if (request.CustomerId != customerUserId)
                return Result<Payment>.Failure("You can only pay your own bills.", ErrorType.Forbidden);

            if (bill.PaymentStatus == PaymentStatus.Paid)
                return Result<Payment>.Failure("This bill has already been paid.", ErrorType.Conflict);

            if (request.Status != RequestStatus.Billed)
                return Result<Payment>.Failure("This request is not awaiting payment.", ErrorType.Conflict);

            if (string.IsNullOrEmpty(bill.RazorpayOrderId) || !string.Equals(bill.RazorpayOrderId, razorpayOrderId, StringComparison.Ordinal))
            {
                _logger.LogWarning(
                    "Razorpay order {OrderId} does not belong to bill #{BillId}", razorpayOrderId, billId);
                return Result<Payment>.Failure(
                    "This payment does not belong to this bill.", ErrorType.Validation);
            }

            return await CompletePaymentAsync(
                bill,
                request,
                method: "Razorpay",
                transactionId: razorpayPaymentId,
                orderId: razorpayOrderId,
                changedByUserId: customerUserId,
                historyNote: $"Payment of ₹{bill.TotalAmount:N2} received via Razorpay. Transaction: {razorpayPaymentId}.",
                failureMessage: "Payment was received but processing failed. " +
                                "Please contact support with transaction ID: " + razorpayPaymentId);
        }

        // ── Razorpay webhook ──────────────────────────────────────────

        public async Task<Result<Payment>> ProcessRazorpayWebhookAsync(RazorpayWebhookEvent webhookEvent)
        {
            if (string.IsNullOrEmpty(webhookEvent.PaymentId))
                return Result<Payment>.Failure("Webhook has no payment id.", ErrorType.Validation);

            var existingPayment = await _uow.Payments.GetByGatewayTransactionIdAsync(webhookEvent.PaymentId);
            if (existingPayment != null)
                return Result<Payment>.Success(existingPayment);

            if (string.IsNullOrEmpty(webhookEvent.OrderId))
                return Result<Payment>.Failure("Webhook has no order id.", ErrorType.BusinessRule);

            // The order id stored at order creation is authoritative; notes/receipt are a fallback hint only.
            var bill = await _uow.Bills.GetByRazorpayOrderIdAsync(webhookEvent.OrderId);
            if (bill == null)
                return Result<Payment>.Failure(
                    "Webhook payment cannot be correlated to a bill.", ErrorType.BusinessRule);
            var billId = bill.Id;

            if (bill.PaymentStatus == PaymentStatus.Paid)
                return Result<Payment>.Failure("This bill has already been paid.", ErrorType.Conflict);

            var expectedPaise = (long)decimal.Round(bill.TotalAmount * 100m, 0, MidpointRounding.AwayFromZero);
            if (webhookEvent.AmountPaise != expectedPaise)
            {
                _logger.LogWarning(
                    "Razorpay webhook amount mismatch for bill #{BillId}: expected {Expected} paise, got {Actual}",
                    billId, expectedPaise, webhookEvent.AmountPaise);
                return Result<Payment>.Failure("Paid amount does not match the bill.", ErrorType.BusinessRule);
            }

            var request = await _uow.ServiceRequests.GetByIdAsync(bill.ServiceRequestId);
            if (request == null)
                return Result<Payment>.Failure("Request not found.", ErrorType.NotFound);
            if (request.Status != RequestStatus.Billed)
                return Result<Payment>.Failure("This request is not awaiting payment.", ErrorType.Conflict);

            return await CompletePaymentAsync(
                bill,
                request,
                method: "Razorpay",
                transactionId: webhookEvent.PaymentId,
                orderId: webhookEvent.OrderId,
                changedByUserId: request.CustomerId,
                historyNote: $"Payment of ₹{bill.TotalAmount:N2} confirmed by Razorpay webhook. " +
                             $"Transaction: {webhookEvent.PaymentId}.",
                failureMessage: "Webhook payment could not be recorded.");
        }

        // ── Completion transaction ────────────────────────────────────

        private async Task<Result<Payment>> CompletePaymentAsync(
            Bill bill,
            ServiceRequest request,
            string method,
            string transactionId,
            string? orderId,
            string changedByUserId,
            string historyNote,
            string failureMessage)
        {
            var now = DateTime.UtcNow;
            var payment = new Payment
            {
                BillId = bill.Id,
                Amount = bill.TotalAmount,
                PaymentMethod = method,
                GatewayTransactionId = transactionId,
                GatewayOrderId = orderId,
                PaidAt = now
            };

            TechnicianProfile? tech = null;

            await _uow.BeginTransactionAsync();
            try
            {
                await _uow.Payments.AddAsync(payment);

                bill.PaymentStatus = PaymentStatus.Paid;
                bill.PaidAt = now;
                _uow.Bills.Update(bill);

                request.Status = RequestStatus.Completed;
                request.UpdatedAt = now;
                _uow.ServiceRequests.Update(request);

                if (request.AssignedTechnicianProfileId.HasValue)
                {
                    tech = await _uow.TechnicianProfiles.GetWithUserAsync(request.AssignedTechnicianProfileId.Value);
                    if (tech != null)
                    {
                        tech.Status = TechnicianStatus.Available;
                        tech.TotalJobsCompleted += 1;
                        _uow.TechnicianProfiles.Update(tech);
                    }
                }

                await _uow.ServiceHistories.AddAsync(new ServiceHistory
                {
                    RequestId = bill.ServiceRequestId,
                    Status = RequestStatus.Completed,
                    ChangedByUserId = changedByUserId,
                    Note = historyNote,
                    ChangedAt = now
                });

                await _uow.CommitTransactionAsync();
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();

                // A concurrent checkout-verify / webhook may have recorded the same
                // gateway transaction first (unique index) — treat as idempotent success.
                var raced = await TryGetExistingAsync(transactionId);
                if (raced != null)
                    return Result<Payment>.Success(raced);

                _logger.LogError(ex, "Payment completion failed for bill #{BillId}", bill.Id);
                return Result<Payment>.Failure(failureMessage, ErrorType.Unexpected);
            }

            _logger.LogInformation(
                "{Method} payment {TransactionId} processed. Bill #{BillId} paid. Request #{RequestId} completed.",
                method, transactionId, bill.Id, bill.ServiceRequestId);

            await RunPostPaymentSideEffectsAsync(bill, request, payment, tech);
            return Result<Payment>.Success(payment);
        }

        private async Task<Payment?> TryGetExistingAsync(string transactionId)
        {
            try
            {
                return await _uow.Payments.GetByGatewayTransactionIdAsync(transactionId);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Idempotency lookup failed for {TransactionId}", transactionId);
                return null;
            }
        }

        private async Task RunPostPaymentSideEffectsAsync(
            Bill bill, ServiceRequest request, Payment payment, TechnicianProfile? tech)
        {
            // Awaited (not fire-and-forget): it shares this scope's DbContext.
            try
            {
                await _promotions.AwardPointsAsync(request.CustomerId, bill.ServiceRequestId, bill.TotalAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Awarding loyalty points failed for request #{RequestId}", bill.ServiceRequestId);
            }

            try
            {
                var customer = await _uow.Users.GetByIdAsync(request.CustomerId);
                if (customer?.Email != null)
                {
                    // E-mail sending does not touch the DbContext; safe to run in the background.
                    _ = _email.SendPaymentReceiptToCustomerAsync(
                        customer.Email,
                        customer.FullName,
                        bill.ServiceRequestId,
                        bill.Id,
                        bill.TotalAmount,
                        payment.PaymentMethod,
                        payment.GatewayTransactionId ?? "N/A",
                        payment.PaidAt);
                }

                if (tech?.User?.Email != null)
                {
                    _ = _email.SendPaymentReceiptToTechnicianAsync(
                        tech.User.Email,
                        tech.User.FullName,
                        bill.ServiceRequestId,
                        bill.TotalAmount);
                }

                if (tech != null)
                    await _notifications.NotifyTechnicianJobCompletedAsync(
                        tech.UserId, bill.ServiceRequestId, bill.TotalAmount);

                await _notifications.NotifyAdminStatusChangedAsync(bill.ServiceRequestId, "Completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Post-payment notifications failed for bill #{BillId}", bill.Id);
            }
        }
    }
}
