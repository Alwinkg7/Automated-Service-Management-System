// ServiceApp.Core/Entities/StatusHistory.cs
using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Entities;

public class StatusHistory
{
    public int Id { get; set; }
    public int ServiceRequestId { get; set; }
    //public ServiceStatus NewStatus { get; set; }
    public string? Note { get; set; }
    public DateTime ChangedAt { get; set; } = DateTime.UtcNow;
    public string? ChangedById { get; set; }

    // Navigation
    public ServiceRequest ServiceRequest { get; set; } = null!;
}