using ServiceApp.Core.Common;

namespace ServiceApp.Core.Interfaces;

/// <summary>Live location sharing from the assigned technician to the customer.</summary>
public interface ITrackingService
{
    /// <summary>
    /// Pushes a location update to the customer of an InProgress request. Only the
    /// assigned technician may call it (otherwise <see cref="ErrorType.Forbidden"/>).
    /// </summary>
    Task<Result> PushLocationAsync(string technicianUserId, int requestId, double lat, double lng);
}
