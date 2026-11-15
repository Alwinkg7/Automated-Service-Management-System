using ServiceApp.Core.Models.Reports;

namespace ServiceApp.Core.Interfaces;

/// <summary>Admin analytics dashboard metrics (the same figures for the MVC page and the API).</summary>
public interface IAnalyticsQueries
{
    Task<AnalyticsSnapshot> GetSnapshotAsync(CancellationToken ct = default);
}
