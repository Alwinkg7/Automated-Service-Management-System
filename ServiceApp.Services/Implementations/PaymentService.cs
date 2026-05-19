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

        public PaymentService(IUnitOfWork uow, ILogger<PaymentService> logger)
        {
            _uow = uow;
            _logger = logger;
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
                .GetByIdAsync(bill.RequestId);
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
                    RequestId = bill.RequestId,
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

                _logger.LogInformation(
                    "Payment processed for bill #{BillId}. " +
                    "Request #{RequestId} completed. " +
                    "Amount: ₹{Amount}",
                    billId, bill.RequestId, bill.TotalAmount);

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
    }
}