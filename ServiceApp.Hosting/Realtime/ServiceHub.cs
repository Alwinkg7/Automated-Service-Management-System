using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Hosting.Realtime;

/// <summary>
/// Realtime hub mapped at <c>/hubs/service</c> by both hosts. Group membership is
/// derived from the authenticated principal (cookie in MVC, JWT in the API) —
/// never from client-supplied values.
/// </summary>
[Authorize]
public class ServiceHub : Hub
{
    private const string AdminRole = "Admin";
    private const string TechnicianRole = "Technician";
    private const string CustomerRole = "Customer";

    private readonly IUnitOfWork _uow;
    private readonly ILogger<ServiceHub> _logger;

    public ServiceHub(IUnitOfWork uow, ILogger<ServiceHub> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public override async Task OnConnectedAsync()
    {
        await JoinOwnGroupsAsync();
        await base.OnConnectedAsync();
    }

    /// <summary>
    /// Kept for backward compatibility with existing clients that call
    /// <c>JoinGroup(role, userId)</c> after connecting. The arguments are ignored;
    /// groups come from the caller's claims (already joined on connect).
    /// </summary>
    public Task JoinGroup(string role, string userId) => JoinOwnGroupsAsync();

    /// <summary>Technicians toggle membership of the "technicians-available" broadcast group.</summary>
    [Authorize(Roles = TechnicianRole)]
    public Task UpdateAvailability(bool isAvailable) =>
        isAvailable
            ? Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.TechniciansAvailable)
            : Groups.RemoveFromGroupAsync(Context.ConnectionId, HubGroups.TechniciansAvailable);

    /// <summary>
    /// Assigned technician pushes GPS coordinates for an in-progress job. The
    /// customer is resolved server-side; <paramref name="customerId"/> is ignored
    /// and kept only for signature compatibility (SignalR requires all arguments).
    /// </summary>
    [Authorize(Roles = TechnicianRole)]
    public async Task ShareLocation(int requestId, double lat, double lng, string customerId)
    {
        var customer = await ResolveCustomerForAssignedJobAsync(requestId, requireInProgress: true);
        if (customer == null) return;

        await Clients.Group(HubGroups.Customer(customer))
            .SendAsync(HubEvents.LocationUpdate, new { requestId, lat, lng, timestamp = DateTime.UtcNow });
    }

    /// <summary>Assigned technician signals the customer map to stop updating.</summary>
    [Authorize(Roles = TechnicianRole)]
    public async Task StopSharing(int requestId, string customerId)
    {
        var customer = await ResolveCustomerForAssignedJobAsync(requestId, requireInProgress: false);
        if (customer == null) return;

        await Clients.Group(HubGroups.Customer(customer))
            .SendAsync(HubEvents.LocationStopped, new { requestId });
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private async Task JoinOwnGroupsAsync()
    {
        var userId = Context.UserIdentifier;
        var user = Context.User;
        if (string.IsNullOrEmpty(userId) || user == null) return;

        foreach (var role in new[] { AdminRole, TechnicianRole, CustomerRole })
        {
            if (!user.IsInRole(role)) continue;
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Personal(role, userId));
        }

        if (user.IsInRole(AdminRole))
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.Admin);

        if (user.IsInRole(TechnicianRole))
            await Groups.AddToGroupAsync(Context.ConnectionId, HubGroups.TechniciansAvailable);

        _logger.LogDebug("SignalR {ConnectionId} joined groups for user {UserId}", Context.ConnectionId, userId);
    }

    private async Task<string?> ResolveCustomerForAssignedJobAsync(int requestId, bool requireInProgress)
    {
        var userId = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        if (userId == null) return null;

        var tech = await _uow.TechnicianProfiles.GetByUserIdAsync(userId);
        var request = await _uow.ServiceRequests.GetByIdAsync(requestId);

        if (tech == null || request == null
            || request.AssignedTechnicianProfileId != tech.TechnicianProfileId
            || (requireInProgress && request.Status != RequestStatus.InProgress))
        {
            _logger.LogWarning(
                "SignalR location call rejected for request #{RequestId} by user {UserId}", requestId, userId);
            return null;
        }

        return request.CustomerId;
    }
}
