using ServiceApp.Core.Entities;

namespace ServiceApp.Web.Areas.Customer.Models
{
    public class CustomerDashboardViewModel
    {
        // Quick stat numbers shown in cards
        public int TotalRequests { get; set; }
        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }
        public int CompletedCount { get; set; }

        // 5 most recent requests shown on dashboard
        public List<ServiceRequest> RecentRequests { get; set; }
            = new List<ServiceRequest>();

        // If false → show "Complete your profile" banner
        public bool IsProfileComplete { get; set; }
    }
}