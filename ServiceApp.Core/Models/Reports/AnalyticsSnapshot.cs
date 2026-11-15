namespace ServiceApp.Core.Models.Reports;

/// <summary>Admin analytics dashboard metrics, computed with SQL aggregates.</summary>
public class AnalyticsSnapshot
{
    public int TotalRequests { get; init; }
    public int CompletedRequests { get; init; }
    public int PendingRequests { get; init; }
    public int TotalCustomers { get; init; }
    public int TotalTechnicians { get; init; }
    public decimal TotalRevenue { get; init; }

    /// <summary>Request count per RequestStatus name (every status present, zero-filled).</summary>
    public IReadOnlyDictionary<string, int> StatusCounts { get; init; } = new Dictionary<string, int>();

    /// <summary>Paid revenue for each of the last 7 days, oldest first.</summary>
    public IReadOnlyList<LabelledAmount> RevenueLast7Days { get; init; } = [];

    /// <summary>Requests created on each of the last 7 days, oldest first.</summary>
    public IReadOnlyList<LabelledCount> RequestsLast7Days { get; init; } = [];

    /// <summary>Request count per category, most frequent first.</summary>
    public IReadOnlyList<LabelledCount> Categories { get; init; } = [];

    public IReadOnlyList<TechnicianSummary> TopTechnicians { get; init; } = [];
    public IReadOnlyList<RecentActivityItem> RecentActivity { get; init; } = [];
}

/// <summary>A labelled point in a time series.</summary>
public record LabelledAmount(string Label, decimal Amount);

/// <summary>A labelled count, for example a category and its request count.</summary>
public record LabelledCount(string Label, int Count);

/// <summary>Latest status change for the analytics activity feed.</summary>
public record RecentActivityItem(int RequestId, string Status, string Note, DateTime ChangedAt, string Category);

/// <summary>Totals for the finished (Completed or Cancelled) requests of one customer.</summary>
public record CustomerHistorySummary(int TotalJobs, int CompletedJobs, int CancelledJobs, decimal TotalSpent, double AverageRating);
