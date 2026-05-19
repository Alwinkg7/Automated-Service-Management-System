// =================================================================
//  RequestListViewModel.cs
//
//  Powers the "My Requests" page.
//  Groups requests by status so customer can see their
//  active ones at the top and history below.
// =================================================================

using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Web.Areas.Customer.Models
{
    public class RequestListViewModel
    {
        // All requests for display
        public List<ServiceRequest> AllRequests { get; set; }
            = new List<ServiceRequest>();

        // Active = anything not yet Completed or Cancelled
        // Shown at the top — these need customer attention
        public List<ServiceRequest> ActiveRequests =>
            AllRequests
                .Where(r => r.Status != RequestStatus.Completed
                         && r.Status != RequestStatus.Cancelled)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

        // Past = Completed or Cancelled
        // Shown below as history
        public List<ServiceRequest> PastRequests =>
            AllRequests
                .Where(r => r.Status == RequestStatus.Completed
                         || r.Status == RequestStatus.Cancelled)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

        // Filter currently applied (null = show all)
        public RequestStatus? CurrentFilter { get; set; }

        // Quick counts for the filter tabs
        public int PendingCount =>
            AllRequests.Count(r => r.Status == RequestStatus.Pending);
        public int InProgressCount =>
            AllRequests.Count(r => r.Status == RequestStatus.InProgress);
        public int CompletedCount =>
            AllRequests.Count(r => r.Status == RequestStatus.Completed);
    }
}