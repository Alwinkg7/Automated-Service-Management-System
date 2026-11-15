using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Interfaces;
using ServiceApp.Web.Areas.Admin.Models;

namespace ServiceApp.Web.Areas.Admin.Controllers
{
    /// <summary>
    /// Admin analytics dashboard. Figures come from <see cref="IAnalyticsQueries"/>
    /// (SQL aggregates), the same source as GET /api/admin/analytics.
    /// </summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AnalyticsController : Controller
    {
        private readonly IAnalyticsQueries _analytics;

        public AnalyticsController(IAnalyticsQueries analytics) => _analytics = analytics;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var s = await _analytics.GetSnapshotAsync(HttpContext.RequestAborted);
            int Status(string name) => s.StatusCounts.TryGetValue(name, out var n) ? n : 0;

            var vm = new AnalyticsViewModel
            {
                TotalRequests = s.TotalRequests,
                CompletedRequests = s.CompletedRequests,
                PendingRequests = s.PendingRequests,
                TotalCustomers = s.TotalCustomers,
                TotalTechnicians = s.TotalTechnicians,
                TotalRevenue = s.TotalRevenue,
                StatusPending = Status("Pending"),
                StatusAssigned = Status("Assigned"),
                StatusInProgress = Status("InProgress"),
                StatusBilled = Status("Billed"),
                StatusCompleted = Status("Completed"),
                StatusCancelled = Status("Cancelled"),
                RevenueDates = s.RevenueLast7Days.Select(x => x.Label).ToList(),
                RevenueValues = s.RevenueLast7Days.Select(x => x.Amount).ToList(),
                RequestDates = s.RequestsLast7Days.Select(x => x.Label).ToList(),
                RequestCounts = s.RequestsLast7Days.Select(x => x.Count).ToList(),
                CategoryLabels = s.Categories.Select(x => x.Label).ToList(),
                CategoryCounts = s.Categories.Select(x => x.Count).ToList(),
                TopTechnicians = s.TopTechnicians.Select(t => new TechnicianStat
                {
                    FullName = t.FullName,
                    Skill = t.Skill.ToString(),
                    TotalJobsCompleted = t.TotalJobsCompleted,
                    Rating = t.Rating,
                    Status = t.Status.ToString(),
                    TotalEarned = t.TotalEarned
                }).ToList(),
                RecentActivities = s.RecentActivity.Select(a => new RecentActivity
                {
                    RequestId = a.RequestId,
                    Status = a.Status,
                    Note = a.Note,
                    ChangedAt = a.ChangedAt,
                    Category = a.Category
                }).ToList()
            };

            return View(vm);
        }
    }
}
