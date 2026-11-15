using ServiceApp.Core.Models.Queries;
using ServiceApp.Core.Models.Reports;

namespace ServiceApp.Core.Interfaces;

/// <summary>Read-only technician queries for admin screens (SQL-side filtering, paging and sums).</summary>
public interface ITechnicianQueries
{
    Task<PagedResult<TechnicianSummary>> SearchAsync(TechnicianSearchCriteria criteria, CancellationToken ct = default);

    Task<TechnicianSummary?> GetSummaryAsync(int technicianProfileId, CancellationToken ct = default);

    Task<TechnicianStatusCounts> CountByStatusAsync(CancellationToken ct = default);

    /// <summary>Paid gross earnings per month for the last <paramref name="months"/> months, oldest first.</summary>
    Task<IReadOnlyList<LabelledAmount>> GetMonthlyEarningsAsync(int technicianProfileId, int months, CancellationToken ct = default);
}
