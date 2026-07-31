// ServiceApp.Core/Interfaces/IServiceRequestService.cs
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Interfaces;

public interface IServiceRequestService
{
    // ── These signatures match ServiceRequestService exactly ─────

    Task<Result<ServiceRequest>> CreateRequestAsync(
        string customerId, string description, ServiceCategory category,
        string address, string pinCode, DateTime preferredDateTime);

    Task<Result<ServiceRequest>> AssignTechnicianAsync(
        int requestId, int technicianProfileId, string adminUserId);

    Task<Result<ServiceRequest>> AcceptJobAsync(
        int requestId, string technicianUserId);

    Task<Result> RejectJobAsync(
        int requestId, string technicianUserId);

    Task<Result> CancelRequestAsync(
        int requestId, string cancelledByUserId);

    Task<Result> SubmitRatingAsync(
        int requestId, string customerId, int rating, string? feedback);

    Task<Result<ServiceRequest>> GetRequestDetailsAsync(int requestId);

    Task<Result<IEnumerable<ServiceRequest>>> GetCustomerRequestsAsync(string customerId);

    Task<Result<IEnumerable<ServiceRequest>>> GetAllRequestsAsync(
        RequestStatus? filterStatus = null);

    Task<Result<IEnumerable<ServiceRequest>>> GetTechnicianJobsAsync(int technicianProfileId);

    Task<Result<IEnumerable<TechnicianProfile>>> GetAvailableTechniciansAsync(
        ServiceCategory category);

    // ── Added for Flutter rating screen ──────────────────────────
    Task<Result<bool>> RateRequestAsync(int requestId, int stars, string? comment);
}