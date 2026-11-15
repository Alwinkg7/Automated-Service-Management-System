using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Models.Queries;
using ServiceApp.Core.Models.Reports;

namespace ServiceApp.Core.Interfaces;

/// <summary>
/// Read-only service-request queries. Filtering, sorting, paging and
/// aggregation run in SQL; results are not tracked.
/// </summary>
public interface IRequestQueries
{
    /// <summary>
    /// Newest-first page of requests matching <paramref name="criteria"/>, with
    /// customer, assigned technician (and user), bill and payment loaded.
    /// </summary>
    Task<PagedResult<ServiceRequest>> SearchAsync(RequestSearchCriteria criteria, CancellationToken ct = default);

    /// <summary>Request counts per status (zero-filled), optionally scoped to a customer or technician.</summary>
    Task<IReadOnlyDictionary<RequestStatus, int>> CountByStatusAsync(
        string? customerId = null, int? technicianProfileId = null, CancellationToken ct = default);

    /// <summary>Totals over the Completed and Cancelled requests of a customer.</summary>
    Task<CustomerHistorySummary> GetCustomerHistorySummaryAsync(string customerId, CancellationToken ct = default);
}
