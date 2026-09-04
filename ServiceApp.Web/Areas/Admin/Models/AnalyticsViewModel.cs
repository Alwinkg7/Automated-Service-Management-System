// =================================================================
//  AnalyticsViewModel.cs — ServiceApp.Web/Areas/Admin/Models
//
//  Carries all data the analytics dashboard needs.
//  Built in AnalyticsController, consumed by Analytics/Index.cshtml.
//
//  DATA SOURCES:
//  - ServiceRequests  → counts, status breakdown, daily trend
//  - Bills + Payments → revenue totals
//  - TechnicianProfiles → leaderboard
//  - ServiceCategory enum → category breakdown
// =================================================================

namespace ServiceApp.Web.Areas.Admin.Models
{
    public class AnalyticsViewModel
    {
        // ── Platform overview (top stat cards) ────────────────────
        public int TotalRequests { get; set; }
        public int CompletedRequests { get; set; }
        public int PendingRequests { get; set; }
        public int TotalCustomers { get; set; }
        public int TotalTechnicians { get; set; }
        public decimal TotalRevenue { get; set; }

        // Completion rate = Completed / Total * 100
        public double CompletionRate =>
            TotalRequests == 0
                ? 0
                : Math.Round((double)CompletedRequests
                    / TotalRequests * 100, 1);

        // ── Revenue trend (last 7 days) ────────────────────────────
        // X axis: date labels ["01 Sep", "02 Sep", ...]
        // Y axis: revenue per day
        public List<string> RevenueDates { get; set; } = new();
        public List<decimal> RevenueValues { get; set; } = new();

        // ── Request trend (last 7 days) ────────────────────────────
        public List<string> RequestDates { get; set; } = new();
        public List<int> RequestCounts { get; set; } = new();

        // ── Service category breakdown (donut chart) ───────────────
        public List<string> CategoryLabels { get; set; } = new();
        public List<int> CategoryCounts { get; set; } = new();

        // ── Status breakdown (bar chart) ──────────────────────────
        public int StatusPending { get; set; }
        public int StatusAssigned { get; set; }
        public int StatusInProgress { get; set; }
        public int StatusBilled { get; set; }
        public int StatusCompleted { get; set; }
        public int StatusCancelled { get; set; }

        // ── Technician leaderboard ─────────────────────────────────
        public List<TechnicianStat> TopTechnicians { get; set; } = new();

        // ── Recent activity feed ───────────────────────────────────
        public List<RecentActivity> RecentActivities { get; set; } = new();
    }

    // One row in the technician leaderboard
    public class TechnicianStat
    {
        public string FullName { get; set; } = string.Empty;
        public string Skill { get; set; } = string.Empty;
        public int TotalJobsCompleted { get; set; }
        public decimal Rating { get; set; }
        public decimal TotalEarned { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    // One item in the activity feed
    public class RecentActivity
    {
        public int RequestId { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
        public DateTime ChangedAt { get; set; }
        public string Category { get; set; } = string.Empty;
    }
}