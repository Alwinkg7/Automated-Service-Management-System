// =================================================================
//  ServiceHub.cs — ServiceApp.Web/Hubs
//
//  The SignalR Hub. Think of it as a persistent connection endpoint.
//  Clients connect here and join "groups" based on their role.
//
//  HOW SIGNALR WORKS:
//  1. Browser connects to /hubs/service via WebSocket
//  2. On connect, client calls JoinGroup("customer-{userId}")
//  3. Server stores that connection in that group
//  4. When we want to notify a user, we send to their group
//  5. Their browser receives it and updates the UI instantly
//
//  GROUPS WE USE:
//  customer-{userId}      → for a specific customer
//  technician-{userId}    → for a specific technician
//  admin                  → all admins (dashboard updates)
//  technicians-available  → all available techs (new job alerts)
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace ServiceApp.Web.Hubs
{
    // [Authorize] ensures only logged-in users can connect
    [Authorize]
    public class ServiceHub : Hub
    {
        private readonly ILogger<ServiceHub> _logger;

        public ServiceHub(ILogger<ServiceHub> logger)
        {
            _logger = logger;
        }

        // =============================================================
        //  Called by the browser JS immediately after connecting.
        //  Client passes their role and userId to join the right group.
        //
        //  Groups let us send targeted messages:
        //  - Send to one customer   → Groups.SendAsync("customer-abc123")
        //  - Send to all admins     → Groups.SendAsync("admin")
        //  - Send to all available  → Groups.SendAsync("technicians-available")
        // =============================================================
        public async Task JoinGroup(string role, string userId)
        {
            // Join the personal group (for direct notifications)
            var personalGroup = $"{role.ToLower()}-{userId}";
            await Groups.AddToGroupAsync(Context.ConnectionId, personalGroup);

            // Admins also join the shared admin group
            if (role.Equals("Admin", StringComparison.OrdinalIgnoreCase))
                await Groups.AddToGroupAsync(Context.ConnectionId, "admin");

            // Available technicians join the broadcast group
            // (so we can notify all of them when a new job comes in)
            if (role.Equals("Technician", StringComparison.OrdinalIgnoreCase))
                await Groups.AddToGroupAsync(
                    Context.ConnectionId, "technicians-available");

            _logger.LogInformation(
                "SignalR: {ConnectionId} joined group {Group}",
                Context.ConnectionId, personalGroup);
        }

        // =============================================================
        //  Called when a technician changes their status.
        //  If they go Offline, remove from the available group.
        //  If they go Online, add back to the available group.
        // =============================================================
        public async Task UpdateAvailability(bool isAvailable)
        {
            if (isAvailable)
                await Groups.AddToGroupAsync(
                    Context.ConnectionId, "technicians-available");
            else
                await Groups.RemoveFromGroupAsync(
                    Context.ConnectionId, "technicians-available");
        }

        // =============================================================
        //  Lifecycle — log connections for debugging
        // =============================================================
        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation(
                "SignalR connected: {ConnectionId} | User: {User}",
                Context.ConnectionId,
                Context.User?.Identity?.Name ?? "anonymous");

            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation(
                "SignalR disconnected: {ConnectionId}",
                Context.ConnectionId);

            await base.OnDisconnectedAsync(exception);
        }
    }
}