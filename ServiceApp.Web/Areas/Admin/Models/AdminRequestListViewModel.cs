// =================================================================
//  AdminRequestListViewModel.cs
//
//  Powers the Admin "All Requests" page.
//  Shows every request across all customers with filter tabs.
// =================================================================

using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Web.Areas.Admin.Models
{
    public class AdminRequestListViewModel
    {
        public List<ServiceRequest> Requests { get; set; }
            = new List<ServiceRequest>();

        // Currently active filter tab
        public RequestStatus? CurrentFilter { get; set; }

        // Count per status for the filter tab badges
        public int AllCount { get; set; }
        public int PendingCount { get; set; }
        public int AssignedCount { get; set; }
        public int InProgressCount { get; set; }
        public int BilledCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
    }
}