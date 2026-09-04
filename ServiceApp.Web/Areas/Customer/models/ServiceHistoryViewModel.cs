// =================================================================
//  ServiceHistoryViewModel.cs
//
//  Powers the customer's full service history page.
//  Shows all past requests with technician info, amounts paid,
//  ratings given, and a "Book again" shortcut.
// =================================================================

using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Web.Areas.Customer.Models
{
    public class ServiceHistoryViewModel
    {
        // All completed or cancelled requests
        public List<ServiceRequest> History { get; set; }
            = new List<ServiceRequest>();

        // Quick stats shown at the top
        public int TotalJobs { get; set; }
        public int CompletedJobs { get; set; }
        public int CancelledJobs { get; set; }
        public decimal TotalSpent { get; set; }
        public double AverageRating { get; set; }

        // Optional filter
        public ServiceCategory? CategoryFilter { get; set; }
        public string? SearchTerm { get; set; }
    }

    // Pre-filled data for re-booking a past request
    public class RebookViewModel
    {
        // Carried over from the original request
        public ServiceCategory Category { get; set; }
        public string Description { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string PinCode { get; set; } = string.Empty;

        // New preferred date/time — default to 3hrs from now
        public DateTime PreferredDateTime { get; set; }
            = DateTime.Now.AddHours(3);

        // For the "Book same technician" badge shown in the form
        public string? PreviousTechnicianName { get; set; }
        public int OriginalRequestId { get; set; }
    }
}