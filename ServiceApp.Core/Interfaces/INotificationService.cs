// =================================================================
//  INotificationService.cs — ServiceApp.Core/Interfaces
//
//  Abstracts all SignalR push notification calls.
//  Service layer calls this after every status change.
//
//  WHY ABSTRACT IT?
//  - ServiceRequestService lives in ServiceApp.Services
//  - SignalR (IHubContext) lives in ServiceApp.Web
//  - Services project should not depend on Web project
//  - Solution: define the interface in Core, implement in Web,
//    inject via DI — dependency flows the right way
//
//  NOTIFICATION TYPES:
//  Each method corresponds to a specific event in the lifecycle.
//  The method name describes WHAT happened.
//  The client JS handler describes WHAT TO DO with it.
// =================================================================

namespace ServiceApp.Core.Interfaces
{
    public interface INotificationService
    {
        // ── Technician notifications ───────────────────────────────

        // Fired when admin assigns a technician to a request
        // Target: the specific technician who was assigned
        Task NotifyTechnicianAssignedAsync(
            string technicianUserId,
            int requestId,
            string category,
            string customerName,
            string address);

        // Fired when a new pending request matches an available tech's skill
        // Target: all available technicians (broadcast)
        Task NotifyNewJobAvailableAsync(
            string category,
            int requestId);

        // ── Customer notifications ─────────────────────────────────

        // Fired when technician accepts — customer knows tech is on the way
        // Target: the specific customer who owns the request
        Task NotifyCustomerJobAcceptedAsync(
            string customerUserId,
            int requestId,
            string technicianName,
            string technicianPhone);

        // Fired when technician creates the bill
        // Target: the customer — prompts them to pay
        Task NotifyCustomerBillCreatedAsync(
            string customerUserId,
            int requestId,
            decimal totalAmount);

        // Fired when job is fully completed (payment confirmed)
        // Target: the technician — tells them they're free now
        Task NotifyTechnicianJobCompletedAsync(
            string technicianUserId,
            int requestId,
            decimal amountEarned);

        // ── Admin notifications ────────────────────────────────────

        // Fired when a new request is created
        // Target: all admins — prompts them to assign
        Task NotifyAdminNewRequestAsync(
            int requestId,
            string category,
            string customerName);

        // Fired when any request status changes
        // Target: all admins — keeps dashboard counts accurate
        Task NotifyAdminStatusChangedAsync(
            int requestId,
            string newStatus);
    }
}