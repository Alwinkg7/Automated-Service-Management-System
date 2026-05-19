// =================================================================
//  AdminDashboardViewModel.cs
//
//  Carries all data the Admin Dashboard view needs.
//  Using a ViewModel (not ViewBag) keeps the view strongly
//  typed — compiler catches typos, IntelliSense works.
// =================================================================

using ServiceApp.Core.Entities;

namespace ServiceApp.Web.Areas.Admin.Models
{
    public class AdminDashboardViewModel
    {
        // ── Stat card numbers ─────────────────────────────────────
        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }
        public int CompletedCount { get; set; }
        public int AvailableTechs { get; set; }
        public int TotalTechs { get; set; }
        public int TotalCustomers { get; set; }

        // ── Pending requests table ────────────────────────────────
        // Top 10 oldest pending — these need to be assigned
        public List<ServiceRequest> PendingRequests { get; set; }
            = new List<ServiceRequest>();
    }
}