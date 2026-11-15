using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Models.Reports;

/// <summary>Technician row for admin lists, with SQL-computed earnings and workload.</summary>
public class TechnicianSummary
{
    public int TechnicianProfileId { get; init; }
    public string UserId { get; init; } = string.Empty;
    public string FullName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string Phone { get; init; } = string.Empty;
    public ServiceCategory Skill { get; init; }
    public TechnicianStatus Status { get; init; }
    public decimal Rating { get; init; }
    public int TotalJobsCompleted { get; init; }
    public decimal TotalEarned { get; init; }
    public int ActiveJobCount { get; init; }
    public bool IsActive { get; init; }
    public string? Bio { get; init; }
    public int? YearsOfExperience { get; init; }
    public string? ServiceAreaPinCode { get; init; }
    public int? ServiceZoneId { get; init; }
}

/// <summary>Technician counts per availability status (whole table, unfiltered).</summary>
public record TechnicianStatusCounts(int Total, int Available, int Busy, int Offline);
