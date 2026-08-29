// =================================================================
//  NotificationService.cs — ServiceApp.Web/Services
//
//  Implements INotificationService using SignalR IHubContext.
//
//  IHubContext<ServiceHub> is the server-side handle to push
//  messages to connected clients. We inject it here.
//
//  Each method:
//  1. Builds a notification payload (anonymous object → JSON)
//  2. Calls Clients.Group(...).SendAsync(eventName, payload)
//  3. SignalR delivers it to all connections in that group
//
//  The client JS registers handlers with:
//  connection.on("eventName", function(payload) { ... })
// =================================================================

using Microsoft.AspNetCore.SignalR;
using ServiceApp.Core.Interfaces;
using ServiceApp.Web.Hubs;

namespace ServiceApp.Web.Services
{
    public class NotificationService : INotificationService
    {
        // IHubContext is the server-side SignalR handle
        // Inject it to push messages from outside a Hub method
        private readonly IHubContext<ServiceHub> _hub;
        private readonly ILogger<NotificationService> _logger;

        public NotificationService(
            IHubContext<ServiceHub> hub,
            ILogger<NotificationService> logger)
        {
            _hub = hub;
            _logger = logger;
        }

        // =============================================================
        //  TECHNICIAN: You've been assigned a job
        // =============================================================
        public async Task NotifyTechnicianAssignedAsync(
            string technicianUserId,
            int requestId,
            string category,
            string customerName,
            string address)
        {
            var group = $"technician-{technicianUserId}";
            var payload = new
            {
                requestId,
                category,
                customerName,
                address,
                message = $"New job assigned: {category} for {customerName}",
                timestamp = DateTime.UtcNow
            };

            await _hub.Clients.Group(group)
                .SendAsync("JobAssigned", payload);

            _logger.LogInformation(
                "SignalR → {Group}: JobAssigned #{RequestId}",
                group, requestId);
        }

        // =============================================================
        //  ALL AVAILABLE TECHNICIANS: New job available
        // =============================================================
        public async Task NotifyNewJobAvailableAsync(
            string category,
            int requestId)
        {
            var payload = new
            {
                requestId,
                category,
                message = $"New {category} job available",
                timestamp = DateTime.UtcNow
            };

            await _hub.Clients.Group("technicians-available")
                .SendAsync("NewJobAvailable", payload);

            _logger.LogInformation(
                "SignalR → technicians-available: NewJobAvailable #{RequestId}",
                requestId);
        }

        // =============================================================
        //  CUSTOMER: Your technician accepted and is on the way
        // =============================================================
        public async Task NotifyCustomerJobAcceptedAsync(
            string customerUserId,
            int requestId,
            string technicianName,
            string technicianPhone)
        {
            var group = $"customer-{customerUserId}";
            var payload = new
            {
                requestId,
                technicianName,
                technicianPhone,
                message = $"{technicianName} accepted your job and is on the way!",
                timestamp = DateTime.UtcNow
            };

            await _hub.Clients.Group(group)
                .SendAsync("JobAccepted", payload);

            _logger.LogInformation(
                "SignalR → {Group}: JobAccepted #{RequestId}",
                group, requestId);
        }

        // =============================================================
        //  CUSTOMER: Your bill is ready — please pay
        // =============================================================
        public async Task NotifyCustomerBillCreatedAsync(
            string customerUserId,
            int requestId,
            decimal totalAmount)
        {
            var group = $"customer-{customerUserId}";
            var payload = new
            {
                requestId,
                totalAmount,
                message = $"Your bill of ₹{totalAmount:N2} is ready. Please pay to complete.",
                timestamp = DateTime.UtcNow
            };

            await _hub.Clients.Group(group)
                .SendAsync("BillCreated", payload);

            _logger.LogInformation(
                "SignalR → {Group}: BillCreated #{RequestId} ₹{Amount}",
                group, requestId, totalAmount);
        }

        // =============================================================
        //  TECHNICIAN: Job completed — you're available again
        // =============================================================
        public async Task NotifyTechnicianJobCompletedAsync(
            string technicianUserId,
            int requestId,
            decimal amountEarned)
        {
            var group = $"technician-{technicianUserId}";
            var payload = new
            {
                requestId,
                amountEarned,
                message = $"Job #{requestId} completed! ₹{amountEarned:N2} earned. You are now Available.",
                timestamp = DateTime.UtcNow
            };

            await _hub.Clients.Group(group)
                .SendAsync("JobCompleted", payload);

            // Also re-add to available pool since they're free now
            // Note: client JS also calls UpdateAvailability(true)
            _logger.LogInformation(
                "SignalR → {Group}: JobCompleted #{RequestId}",
                group, requestId);
        }

        // =============================================================
        //  ADMIN: New request needs assignment
        // =============================================================
        public async Task NotifyAdminNewRequestAsync(
            int requestId,
            string category,
            string customerName)
        {
            var payload = new
            {
                requestId,
                category,
                customerName,
                message = $"New {category} request from {customerName}",
                timestamp = DateTime.UtcNow
            };

            await _hub.Clients.Group("admin")
                .SendAsync("NewRequest", payload);

            _logger.LogInformation(
                "SignalR → admin: NewRequest #{RequestId}", requestId);
        }

        // =============================================================
        //  ADMIN: Any status change — update dashboard counts
        // =============================================================
        public async Task NotifyAdminStatusChangedAsync(
            int requestId,
            string newStatus)
        {
            var payload = new
            {
                requestId,
                newStatus,
                message = $"Request #{requestId} is now {newStatus}",
                timestamp = DateTime.UtcNow
            };

            await _hub.Clients.Group("admin")
                .SendAsync("StatusChanged", payload);
        }
    }
}