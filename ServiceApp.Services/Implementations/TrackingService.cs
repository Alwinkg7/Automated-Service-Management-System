using ServiceApp.Core.Common;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Services.Implementations;

/// <summary>Validates and relays technician location updates to the customer.</summary>
public class TrackingService : ITrackingService
{
    private readonly IUnitOfWork _uow;
    private readonly INotificationService _notifications;

    public TrackingService(IUnitOfWork uow, INotificationService notifications)
    {
        _uow = uow;
        _notifications = notifications;
    }

    public async Task<Result> PushLocationAsync(string technicianUserId, int requestId, double lat, double lng)
    {
        if (lat is < -90 or > 90 || lng is < -180 or > 180)
            return Result.Failure("Invalid coordinates.");

        var tech = await _uow.TechnicianProfiles.GetByUserIdAsync(technicianUserId);
        if (tech == null)
            return Result.Failure("Technician profile not found.", ErrorType.NotFound);

        var request = await _uow.ServiceRequests.GetByIdAsync(requestId);
        if (request == null)
            return Result.Failure("Request not found.", ErrorType.NotFound);

        if (request.AssignedTechnicianProfileId != tech.TechnicianProfileId)
            return Result.Failure("This job is not assigned to you.", ErrorType.Forbidden);

        if (request.Status != RequestStatus.InProgress)
            return Result.Failure("Location can only be shared for an InProgress job.", ErrorType.Conflict);

        await _notifications.PushLocationUpdateAsync(request.CustomerId, requestId, lat, lng);
        return Result.Success();
    }
}
