// =================================================================
//  IServiceRequestService.cs — ServiceApp.Core/Interfaces
//
//  The contract for all service request business logic.
//  Controllers depend on this interface — NOT the concrete class.
//
//  WHY A SERVICE LAYER?
//  Controllers should only:
//    1. Read the incoming HTTP request (model binding)
//    2. Call a service method
//    3. Return a view or redirect
//
//  ALL business rules live here:
//    - Can this status transition happen?
//    - Is the technician actually available?
//    - Who is allowed to do this action?
//    - What side effects happen? (history log, status change, etc.)
//
//  If a controller has an if-statement about business logic,
//  that logic belongs in the service layer instead.
// =================================================================

using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Interfaces
{
    public interface IServiceRequestService
    {
        // ── Customer actions ───────────────────────────────────────

        // Create a brand new service request.
        // Sets status = Pending, logs first ServiceHistory row.
        Task<Result<ServiceRequest>> CreateRequestAsync(
            string customerId,
            string description,
            ServiceCategory category,
            string address,
            string pinCode,
            DateTime preferredDateTime);

        // Cancel a request — only allowed when Pending or Assigned.
        // Frees up the technician if one was assigned.
        Task<Result> CancelRequestAsync(
            int requestId,
            string cancelledByUserId);

        // Customer submits a rating after job is Completed.
        // Updates technician's average Rating on TechnicianProfile.
        Task<Result> SubmitRatingAsync(
            int requestId,
            string customerId,
            int rating,
            string? feedback);

        // ── Admin actions ──────────────────────────────────────────

        // Admin manually picks a technician for a Pending request.
        // Validates: request must be Pending, technician must be Available
        //            and skill must match request category.
        // Side effects: Request → Assigned, logs ServiceHistory.
        Task<Result<ServiceRequest>> AssignTechnicianAsync(
            int requestId,
            int technicianProfileId,
            string adminUserId);

        // ── Technician actions ─────────────────────────────────────

        // Technician accepts an Assigned job.
        // Side effects: Request → InProgress,
        //               TechnicianProfile.Status → Busy,
        //               logs ServiceHistory.
        // All three happen in ONE transaction.
        Task<Result<ServiceRequest>> AcceptJobAsync(
            int requestId,
            string technicianUserId);

        // Technician rejects an Assigned job.
        // Side effects: Request → Pending (back to queue),
        //               clears AssignedTechnicianProfileId,
        //               logs ServiceHistory.
        Task<Result> RejectJobAsync(
            int requestId,
            string technicianUserId);

        // ── Shared queries ─────────────────────────────────────────

        // Load a single request with all related data.
        // Used by the Details page for all three roles.
        Task<Result<ServiceRequest>> GetRequestDetailsAsync(
            int requestId);

        // Customer's "My Requests" list — only their own requests.
        Task<Result<IEnumerable<ServiceRequest>>> GetCustomerRequestsAsync(
            string customerId);

        // Admin's view — all requests, optionally filtered by status.
        Task<Result<IEnumerable<ServiceRequest>>> GetAllRequestsAsync(
            RequestStatus? filterStatus = null);

        // Technician's job list — only jobs assigned to them.
        Task<Result<IEnumerable<ServiceRequest>>> GetTechnicianJobsAsync(
            int technicianProfileId);

        // Get available technicians matching a service category.
        // Used by admin on the assign screen.
        Task<Result<IEnumerable<TechnicianProfile>>> GetAvailableTechniciansAsync(
            ServiceCategory category);
    }
}