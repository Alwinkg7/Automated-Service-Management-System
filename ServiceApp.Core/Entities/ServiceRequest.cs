// ServiceApp.Core/Entities/ServiceRequest.cs
using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Entities;

public class ServiceRequest
{
    public int RequestId { get; set; }
    public string CustomerId { get; set; } = string.Empty;
    public int? AssignedTechnicianProfileId { get; set; }
    public ServiceCategory Category { get; set; }
    public RequestStatus Status { get; set; } = RequestStatus.Pending;
    public string Description { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string PinCode { get; set; } = string.Empty;
    public DateTime PreferredDateTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public int? CustomerRating { get; set; }
    public string? CustomerFeedback { get; set; }

    // Navigation
    public ApplicationUser Customer { get; set; } = null!;
    public TechnicianProfile? AssignedTechnician { get; set; }
    public Bill? Bill { get; set; }
    public ICollection<ServiceHistory> History { get; set; } = new List<ServiceHistory>();
}