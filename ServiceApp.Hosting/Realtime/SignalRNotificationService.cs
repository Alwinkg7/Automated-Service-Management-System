using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Hosting.Realtime;

/// <summary>
/// <see cref="INotificationService"/> backed by <see cref="ServiceHub"/>. Without a
/// Redis backplane a message only reaches clients connected to the host that
/// raised it; with <c>SignalR:RedisConnectionString</c> configured it reaches both.
/// </summary>
public class SignalRNotificationService : INotificationService
{
    private readonly IHubContext<ServiceHub> _hub;
    private readonly ILogger<SignalRNotificationService> _logger;

    public SignalRNotificationService(IHubContext<ServiceHub> hub, ILogger<SignalRNotificationService> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public Task NotifyTechnicianAssignedAsync(
        string technicianUserId, int requestId, string category, string customerName, string address) =>
        SendAsync(HubGroups.Technician(technicianUserId), HubEvents.JobAssigned, requestId, new
        {
            requestId,
            category,
            customerName,
            address,
            message = $"New job assigned: {category} for {customerName}",
            timestamp = DateTime.UtcNow
        });

    public Task NotifyNewJobAvailableAsync(string category, int requestId) =>
        SendAsync(HubGroups.TechniciansAvailable, HubEvents.NewJobAvailable, requestId, new
        {
            requestId,
            category,
            message = $"New {category} job available",
            timestamp = DateTime.UtcNow
        });

    public Task NotifyCustomerJobAcceptedAsync(
        string customerUserId, int requestId, string technicianName, string technicianPhone) =>
        SendAsync(HubGroups.Customer(customerUserId), HubEvents.JobAccepted, requestId, new
        {
            requestId,
            technicianName,
            technicianPhone,
            message = $"{technicianName} accepted your job and is on the way!",
            timestamp = DateTime.UtcNow
        });

    public Task NotifyCustomerBillCreatedAsync(string customerUserId, int requestId, decimal totalAmount) =>
        SendAsync(HubGroups.Customer(customerUserId), HubEvents.BillCreated, requestId, new
        {
            requestId,
            totalAmount,
            message = $"Your bill of ₹{totalAmount:N2} is ready. Please pay to complete.",
            timestamp = DateTime.UtcNow
        });

    public Task NotifyTechnicianJobCompletedAsync(string technicianUserId, int requestId, decimal amountEarned) =>
        SendAsync(HubGroups.Technician(technicianUserId), HubEvents.JobCompleted, requestId, new
        {
            requestId,
            amountEarned,
            message = $"Job #{requestId} completed! ₹{amountEarned:N2} earned. You are now Available.",
            timestamp = DateTime.UtcNow
        });

    public Task NotifyAdminNewRequestAsync(int requestId, string category, string customerName) =>
        SendAsync(HubGroups.Admin, HubEvents.NewRequest, requestId, new
        {
            requestId,
            category,
            customerName,
            message = $"New {category} request from {customerName}",
            timestamp = DateTime.UtcNow
        });

    public Task NotifyAdminStatusChangedAsync(int requestId, string newStatus) =>
        SendAsync(HubGroups.Admin, HubEvents.StatusChanged, requestId, new
        {
            requestId,
            newStatus,
            message = $"Request #{requestId} is now {newStatus}",
            timestamp = DateTime.UtcNow
        });

    public Task PushLocationUpdateAsync(string customerId, int requestId, double lat, double lng) =>
        SendAsync(HubGroups.Customer(customerId), HubEvents.LocationUpdate, requestId, new
        {
            requestId,
            lat,
            lng,
            timestamp = DateTime.UtcNow
        });

    private async Task SendAsync(string group, string eventName, int requestId, object payload)
    {
        await _hub.Clients.Group(group).SendAsync(eventName, payload);
        _logger.LogDebug("SignalR -> {Group}: {Event} #{RequestId}", group, eventName, requestId);
    }
}
