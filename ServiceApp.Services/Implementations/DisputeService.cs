// =================================================================
//  DisputeService.cs
// =================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Services.Implementations
{
    public class DisputeService : IDisputeService
    {
        private readonly IUnitOfWork _uow;
        private readonly IRazorpayService _razorpay;
        private readonly IEmailService _email;
        private readonly ILogger<DisputeService> _logger;

        public DisputeService(
            IUnitOfWork uow,
            IRazorpayService razorpay,
            IEmailService email,
            IConfiguration config,
            ILogger<DisputeService> logger)
        {
            _uow = uow;
            _razorpay = razorpay;
            _email = logger as IEmailService ?? email;
            _logger = logger;

            // Correct assignment
            _email = email;
        }

        // =============================================================
        //  RAISE DISPUTE
        // =============================================================
        public async Task<Result<Dispute>> RaiseDisputeAsync(
            int requestId,
            string raisedByUserId,
            string reason,
            string description)
        {
            // Load request
            var request = await _uow.ServiceRequests
                .GetWithDetailsAsync(requestId);
            if (request == null)
                return Result<Dispute>.Failure("Request not found.");

            // Only Completed requests can be disputed
            if (request.Status != RequestStatus.Completed)
                return Result<Dispute>.Failure(
                    "Disputes can only be raised on completed requests.");

            // Only the customer or assigned technician can raise
            var isCustomer = request.CustomerId == raisedByUserId;
            var isTechnician = request.AssignedTechnician?.UserId
                               == raisedByUserId;

            if (!isCustomer && !isTechnician)
                return Result<Dispute>.Failure(
                    "You can only raise disputes on your own requests.");

            // Only one active dispute per request
            var existing = await _uow.Disputes
                .GetByRequestIdAsync(requestId);
            if (existing != null && existing.Status != "Resolved")
                return Result<Dispute>.Failure(
                    "An active dispute already exists for this request. " +
                    $"Dispute #{existing.DisputeId} is {existing.Status}.");

            var dispute = new Dispute
            {
                RequestId = requestId,
                RaisedByUserId = raisedByUserId,
                Reason = reason,
                Description = description.Trim(),
                Status = "Open",
                RaisedAt = DateTime.UtcNow,
                SlaDeadline = DateTime.UtcNow.AddHours(48)
            };

            await _uow.Disputes.AddAsync(dispute);
            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Dispute #{DisputeId} raised for request #{RequestId} " +
                "by user {UserId}. Reason: {Reason}",
                dispute.DisputeId, requestId, raisedByUserId, reason);

            // Email admin notification (fire and forget)
            _ = _email.SendRequestConfirmationAsync(
                "admin@serviceapp.com",
                "Admin",
                requestId,
                $"DISPUTE #{dispute.DisputeId} — {reason}",
                DateTime.UtcNow.ToString("dd MMM yyyy, hh:mm tt"));

            return Result<Dispute>.Success(dispute);
        }

        // =============================================================
        //  START REVIEW
        // =============================================================
        public async Task<Result<Dispute>> StartReviewAsync(
            int disputeId,
            string adminNote)
        {
            var dispute = await _uow.Disputes
                .GetWithDetailsAsync(disputeId);
            if (dispute == null)
                return Result<Dispute>.Failure("Dispute not found.");

            if (dispute.Status != "Open")
                return Result<Dispute>.Failure(
                    $"Dispute is {dispute.Status}, not Open.");

            dispute.Status = "UnderReview";
            dispute.AdminNote = adminNote?.Trim();
            _uow.Disputes.Update(dispute);
            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Dispute #{DisputeId} is now UnderReview", disputeId);

            return Result<Dispute>.Success(dispute);
        }

        // =============================================================
        //  RESOLVE — no refund
        // =============================================================
        public async Task<Result<Dispute>> ResolveAsync(
            int disputeId,
            string resolution,
            string adminNote)
        {
            var dispute = await _uow.Disputes
                .GetWithDetailsAsync(disputeId);
            if (dispute == null)
                return Result<Dispute>.Failure("Dispute not found.");

            if (dispute.Status == "Resolved")
                return Result<Dispute>.Failure(
                    "Dispute is already resolved.");

            dispute.Status = "Resolved";
            dispute.Resolution = resolution;
            dispute.AdminNote = adminNote?.Trim();
            dispute.ResolvedAt = DateTime.UtcNow;
            _uow.Disputes.Update(dispute);
            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Dispute #{DisputeId} resolved: {Resolution}",
                disputeId, resolution);

            return Result<Dispute>.Success(dispute);
        }

        // =============================================================
        //  ISSUE REFUND via Razorpay + resolve dispute
        //
        //  RAZORPAY REFUND API:
        //  POST /v1/payments/{payment_id}/refund
        //  Body: { amount: paise, notes: { reason: "..." } }
        // =============================================================
        public async Task<Result<Dispute>> IssueRefundAsync(
            int disputeId,
            decimal refundAmount,
            string adminNote)
        {
            var dispute = await _uow.Disputes
                .GetWithDetailsAsync(disputeId);
            if (dispute == null)
                return Result<Dispute>.Failure("Dispute not found.");

            if (dispute.RefundIssued)
                return Result<Dispute>.Failure(
                    "A refund has already been issued for this dispute.");

            // Load the payment
            var payment = dispute.ServiceRequest?.Bill?.Payment;
            if (payment == null)
                return Result<Dispute>.Failure(
                    "No payment found for this request. " +
                    "Cannot issue refund.");

            if (string.IsNullOrEmpty(payment.GatewayTransactionId) ||
                payment.GatewayTransactionId.StartsWith("CASH"))
                return Result<Dispute>.Failure(
                    "Refunds can only be issued for online (Razorpay) payments. " +
                    "This request was paid with cash.");

            var maxRefund = payment.Amount;
            if (refundAmount <= 0 || refundAmount > maxRefund)
                return Result<Dispute>.Failure(
                    $"Refund amount must be between ₹1 and " +
                    $"₹{maxRefund:N2} (original payment amount).");

            // ── Call Razorpay Refund API ────────────────────────────
            string refundId;
            try
            {
                var reason = $"Dispute #{disputeId}: {adminNote}";

                refundId = await _razorpay.RefundPaymentAsync(
                    payment.GatewayTransactionId!,
                    refundAmount,
                    reason);

                _logger.LogInformation(
                    "Razorpay refund {RefundId} issued for dispute " +
                    "#{DisputeId}. Amount: ₹{Amount}",
                    refundId, disputeId, refundAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Razorpay refund failed for dispute #{DisputeId}",
                    disputeId);
                return Result<Dispute>.Failure(
                    "Razorpay refund failed: " + ex.Message +
                    " — Check your Razorpay dashboard.");
            }

            // ── Update dispute record ──────────────────────────────
            dispute.RefundIssued = true;
            dispute.RefundAmount = refundAmount;
            dispute.RazorpayRefundId = refundId;
            dispute.Status = "Resolved";
            dispute.Resolution = "Refunded";
            dispute.AdminNote = adminNote?.Trim();
            dispute.ResolvedAt = DateTime.UtcNow;
            _uow.Disputes.Update(dispute);
            await _uow.SaveChangesAsync();

            // Notify customer by email
            var customer = dispute.ServiceRequest?.Customer;
            if (customer != null)
            {
                _ = _email.SendPaymentReceiptToCustomerAsync(
                    customer.Email!,
                    customer.FullName,
                    dispute.RequestId,
                    dispute.ServiceRequest!.Bill!.Id,
                    refundAmount,
                    "Razorpay Refund",
                    refundId,
                    DateTime.UtcNow);
            }

            return Result<Dispute>.Success(dispute);
        }

        // =============================================================
        //  QUERIES
        // =============================================================
        public async Task<IEnumerable<Dispute>>
            GetAllDisputesAsync(string? statusFilter = null)
        {
            if (!string.IsNullOrEmpty(statusFilter))
                return await _uow.Disputes
                    .GetByStatusAsync(statusFilter);

            return await _uow.Disputes.GetAllWithDetailsAsync();
        }

        public async Task<Dispute?> GetDisputeDetailsAsync(
            int disputeId) =>
            await _uow.Disputes.GetWithDetailsAsync(disputeId);

        public async Task<Dispute?> GetDisputeByRequestIdAsync(
            int requestId) =>
            await _uow.Disputes.GetByRequestIdAsync(requestId);

        public async Task<IEnumerable<Dispute>>
            GetSlaBreachedDisputesAsync() =>
            await _uow.Disputes.GetSlaBreachedAsync();
    }
}