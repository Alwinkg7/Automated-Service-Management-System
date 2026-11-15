using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Models.Queries;

/// <summary>Filters for the admin technician list (translated to SQL).</summary>
public class TechnicianSearchCriteria
{
    public ServiceCategory? Skill { get; init; }
    public TechnicianStatus? Status { get; init; }
    public bool? IsActive { get; init; }

    /// <summary>Matches name, email or phone.</summary>
    public string? Search { get; init; }

    public int Page { get; init; } = 1;

    /// <summary>Page size; 0 returns every match.</summary>
    public int PageSize { get; init; } = 20;
}
