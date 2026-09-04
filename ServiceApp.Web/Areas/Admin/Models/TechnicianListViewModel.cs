// =================================================================
//  TechnicianListViewModel.cs
//
//  Powers the Admin Technicians page.
//  Shows all technicians with their live status, performance
//  stats, and earnings. Filterable by skill and status.
// =================================================================

using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Web.Areas.Admin.Models
{
    public class TechnicianListViewModel
    {
        public List<TechnicianRowItem> Technicians { get; set; }
            = new List<TechnicianRowItem>();

        // Active filters
        public string? SkillFilter { get; set; }
        public TechnicianStatus? StatusFilter { get; set; }

        // Counts for filter tabs
        public int TotalCount { get; set; }
        public int AvailableCount { get; set; }
        public int BusyCount { get; set; }
        public int OfflineCount { get; set; }
    }

    // One row in the technicians table — flat projection so
    // the view never touches navigation properties directly
    public class TechnicianRowItem
    {
        public int TechnicianProfileId { get; set; }
        public string UserId { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public ServiceCategory Skill { get; set; }
        public TechnicianStatus Status { get; set; }
        public decimal Rating { get; set; }
        public int TotalJobsCompleted { get; set; }
        public decimal TotalEarned { get; set; }
        public string? Bio { get; set; }
        public int? YearsOfExperience { get; set; }
        public string? ServiceAreaPinCode { get; set; }
        public bool IsActive { get; set; }

        // Active job count — how many InProgress jobs right now
        public int ActiveJobCount { get; set; }
    }

    // For the technician detail / job history page
    public class TechnicianDetailViewModel
    {
        public TechnicianRowItem Profile { get; set; } = null!;

        // Full job history for this technician
        public List<ServiceApp.Core.Entities.ServiceRequest> JobHistory
        { get; set; } = new List<ServiceApp.Core.Entities.ServiceRequest>();

        // Monthly earnings breakdown (last 6 months)
        public List<string> EarningsMonths { get; set; } = new();
        public List<decimal> EarningsValues { get; set; } = new();
    }
}