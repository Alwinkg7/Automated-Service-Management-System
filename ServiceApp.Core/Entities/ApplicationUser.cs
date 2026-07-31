using Microsoft.AspNetCore.Identity;
using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Entities;

public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public UserRole Role { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation
    public CustomerProfile? CustomerProfile { get; set; }
    public TechnicianProfile? TechnicianProfile { get; set; }
    public AdminProfile? AdminProfile { get; set; }

    public ICollection<ServiceRequest> ServiceRequests { get; set; }
        = new List<ServiceRequest>();
}