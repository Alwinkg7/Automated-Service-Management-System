using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Web.Areas.Technician.Models
{
    public class TechnicianDashboardViewModel
    {
        public int TechnicianProfileId { get; set; }
        public TechnicianStatus CurrentStatus { get; set; }
        public ServiceCategory Skill { get; set; }
        public decimal Rating { get; set; }
        public int TotalJobsCompleted { get; set; }

        // Jobs needing action (Accept or Create Bill)
        public List<ServiceRequest> ActiveJobs { get; set; }
            = new List<ServiceRequest>();

        // Last 5 completed/cancelled jobs
        public List<ServiceRequest> RecentPastJobs { get; set; }
            = new List<ServiceRequest>();

        // Stats
        public int TotalAssigned { get; set; }
        public int CompletedCount { get; set; }

        // If false → show "Complete your profile" banner
        public bool IsProfileComplete { get; set; }
    }
}