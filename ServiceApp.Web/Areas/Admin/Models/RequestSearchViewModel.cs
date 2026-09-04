// =================================================================
//  RequestSearchViewModel.cs
//
//  Powers the enhanced All Requests page.
//  Combines search, date filter, status filter, and pagination
//  all in one ViewModel so the view stays clean.
// =================================================================

using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Web.Areas.Admin.Models
{
    public class RequestSearchViewModel
    {
        // ── Search + filter inputs ─────────────────────────────────
        // Search by customer name, description, or address
        public string? SearchTerm { get; set; }
        public string? StatusFilter { get; set; }
        public string? DateRange { get; set; } // "today","week","month","custom"
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }

        // ── Pagination ─────────────────────────────────────────────
        public int CurrentPage { get; set; } = 1;
        public int PageSize { get; set; } = 15;
        public int TotalCount { get; set; }
        public int TotalPages =>
            (int)Math.Ceiling((double)TotalCount / PageSize);

        public bool HasPrevious => CurrentPage > 1;
        public bool HasNext => CurrentPage < TotalPages;

        // ── Results ────────────────────────────────────────────────
        public List<ServiceRequest> Requests { get; set; }
            = new List<ServiceRequest>();

        // ── Counts for filter tabs ─────────────────────────────────
        public int AllCount { get; set; }
        public int PendingCount { get; set; }
        public int AssignedCount { get; set; }
        public int InProgressCount { get; set; }
        public int BilledCount { get; set; }
        public int CompletedCount { get; set; }
        public int CancelledCount { get; set; }
    }
}