using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Models.Queries;

/// <summary>
/// Filters for service-request lists. Every filter is translated to SQL
/// (no in-memory filtering). Null means no filter.
/// </summary>
public class RequestSearchCriteria
{
    public string? CustomerId { get; init; }
    public int? TechnicianProfileId { get; init; }
    public IReadOnlyCollection<RequestStatus>? Statuses { get; init; }
    public ServiceCategory? Category { get; init; }

    /// <summary>Matches customer name, technician name, description, address, pin code or an exact request id.</summary>
    public string? Search { get; init; }

    /// <summary>Inclusive UTC date range on CreatedAt (whole days).</summary>
    public DateTime? CreatedFrom { get; init; }
    public DateTime? CreatedTo { get; init; }

    public int Page { get; init; } = 1;

    /// <summary>Page size; 0 returns every match (CSV export).</summary>
    public int PageSize { get; init; } = 20;

    public const int MaxPageSize = 100;

    /// <summary>Resolves the date-range shortcuts today, week, month and custom to an inclusive from/to pair.</summary>
    public static (DateTime? From, DateTime? To) ResolveDateRange(string? range, DateTime? from, DateTime? to)
    {
        var today = DateTime.UtcNow.Date;
        return range switch
        {
            "today" => (today, today),
            "week" => (today.AddDays(-6), today),
            "month" => (today.AddDays(-29), today),
            "custom" or null or "" => (from, to),
            _ => (null, null)
        };
    }
}
