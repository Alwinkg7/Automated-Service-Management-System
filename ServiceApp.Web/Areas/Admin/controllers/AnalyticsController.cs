// =================================================================
//  AnalyticsController.cs — ServiceApp.Web/Areas/Admin/Controllers
//
//  Builds the analytics data from the database and passes it
//  to the view as a strongly typed ViewModel.
//
//  All calculations happen here — the view only displays data.
//
//  GET /Admin/Analytics/Index → full analytics dashboard
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Web.Areas.Admin.Models;

namespace ServiceApp.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class AnalyticsController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<AnalyticsController> _logger;

        public AnalyticsController(
            IUnitOfWork uow,
            ILogger<AnalyticsController> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // GET /Admin/Analytics/Index
        public async Task<IActionResult> Index()
        {
            var vm = new AnalyticsViewModel();

            // ── Load all data in parallel ─────────────────────────
            var allRequests = (await _uow.ServiceRequests
                .GetAllWithDetailsAsync()).ToList();
            var allTechnicians = (await _uow.TechnicianProfiles
                .GetAllWithUsersAsync()).ToList();
            var allCustomers = (await _uow.Users
                .GetAllByRoleAsync(UserRole.Customer)).ToList();
            var allHistories = (await _uow.ServiceHistories
                .GetAllAsync()).ToList();

            // ── Stat cards ────────────────────────────────────────
            vm.TotalRequests = allRequests.Count;
            vm.CompletedRequests = allRequests.Count(r =>
                r.Status == RequestStatus.Completed);
            vm.PendingRequests = allRequests.Count(r =>
                r.Status == RequestStatus.Pending);
            vm.TotalCustomers = allCustomers.Count;
            vm.TotalTechnicians = allTechnicians.Count;

            // Total revenue = sum of all paid bills
            vm.TotalRevenue = allRequests
                .Where(r => r.Bill != null
                         && r.Bill.PaymentStatus == PaymentStatus.Paid)
                .Sum(r => r.Bill!.TotalAmount);

            // ── Status breakdown ──────────────────────────────────
            vm.StatusPending = allRequests.Count(r =>
                r.Status == RequestStatus.Pending);
            vm.StatusAssigned = allRequests.Count(r =>
                r.Status == RequestStatus.Assigned);
            vm.StatusInProgress = allRequests.Count(r =>
                r.Status == RequestStatus.InProgress);
            vm.StatusBilled = allRequests.Count(r =>
                r.Status == RequestStatus.Billed);
            vm.StatusCompleted = allRequests.Count(r =>
                r.Status == RequestStatus.Completed);
            vm.StatusCancelled = allRequests.Count(r =>
                r.Status == RequestStatus.Cancelled);

            // ── Revenue trend — last 7 days ───────────────────────
            // For each day: sum of bills paid on that day
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.UtcNow.Date.AddDays(-i);
                var dateLabel = date.ToString("dd MMM");
                var dayRevenue = allRequests
                    .Where(r => r.Bill != null
                             && r.Bill.PaidAt.HasValue
                             && r.Bill.PaidAt.Value.Date == date)
                    .Sum(r => r.Bill!.TotalAmount);

                vm.RevenueDates.Add(dateLabel);
                vm.RevenueValues.Add(dayRevenue);
            }

            // ── Request trend — last 7 days ───────────────────────
            // For each day: count of requests created
            for (int i = 6; i >= 0; i--)
            {
                var date = DateTime.UtcNow.Date.AddDays(-i);
                var dateLabel = date.ToString("dd MMM");
                var count = allRequests.Count(r =>
                    r.CreatedAt.Date == date);

                vm.RequestDates.Add(dateLabel);
                vm.RequestCounts.Add(count);
            }

            // ── Category breakdown ────────────────────────────────
            var categoryGroups = allRequests
                .GroupBy(r => r.Category)
                .OrderByDescending(g => g.Count());

            foreach (var group in categoryGroups)
            {
                vm.CategoryLabels.Add(group.Key.ToString());
                vm.CategoryCounts.Add(group.Count());
            }

            // ── Technician leaderboard (top 5) ────────────────────
            vm.TopTechnicians = allTechnicians
                .OrderByDescending(t => t.TotalJobsCompleted)
                .ThenByDescending(t => t.Rating)
                .Take(5)
                .Select(t => new TechnicianStat
                {
                    FullName = t.User.FullName,
                    Skill = t.Skill.ToString(),
                    TotalJobsCompleted = t.TotalJobsCompleted,
                    Rating = t.Rating,
                    Status = t.Status.ToString(),
                    // Earnings = sum of paid bills this tech created
                    TotalEarned = allRequests
                        .Where(r => r.AssignedTechnicianProfileId
                                 == t.TechnicianProfileId
                                 && r.Bill != null
                                 && r.Bill.PaymentStatus
                                 == PaymentStatus.Paid)
                        .Sum(r => r.Bill!.TotalAmount)
                })
                .ToList();

            // ── Recent activity (last 10 history entries) ─────────
            vm.RecentActivities = allHistories
                .OrderByDescending(h => h.ChangedAt)
                .Take(10)
                .Select(h =>
                {
                    var req = allRequests.FirstOrDefault(
                        r => r.RequestId == h.RequestId);
                    return new RecentActivity
                    {
                        RequestId = h.RequestId,
                        Status = h.Status.ToString(),
                        Note = h.Note ?? string.Empty,
                        ChangedAt = h.ChangedAt,
                        Category = req?.Category.ToString() ?? "—"
                    };
                })
                .ToList();

            return View(vm);
        }
    }
}