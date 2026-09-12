// =================================================================
//  IDisputeService.cs — ServiceApp.Core/Interfaces
//
//  All dispute business logic.
//  Called by Customer/Technician controllers to raise disputes
//  and by Admin controller to review and resolve them.
// =================================================================

using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;

namespace ServiceApp.Core.Interfaces
{
    public interface IDisputeService
    {
        // Raise a new dispute on a completed request.
        // Only the customer or assigned technician can raise.
        // Only one active dispute per request.
        Task<Result<Dispute>> RaiseDisputeAsync(
            int requestId,
            string raisedByUserId,
            string reason,
            string description);

        // Admin marks dispute as UnderReview
        Task<Result<Dispute>> StartReviewAsync(
            int disputeId,
            string adminNote);

        // Admin resolves — close with no action
        Task<Result<Dispute>> ResolveAsync(
            int disputeId,
            string resolution,   // "Refunded","Rejected","ClosedNoAction"
            string adminNote);

        // Admin issues a Razorpay refund + resolves dispute
        Task<Result<Dispute>> IssueRefundAsync(
            int disputeId,
            decimal refundAmount,
            string adminNote);

        // Queries
        Task<IEnumerable<Dispute>> GetAllDisputesAsync(
            string? statusFilter = null);

        Task<Dispute?> GetDisputeDetailsAsync(int disputeId);
        Task<Dispute?> GetDisputeByRequestIdAsync(int requestId);
        Task<IEnumerable<Dispute>> GetSlaBreachedDisputesAsync();
    }
}