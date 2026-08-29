// =================================================================
//  Areas/Admin/Controllers/HomeController.cs
//
//  [Area("Admin")]    → maps to /Admin/Home/Dashboard route
//  [Authorize(Roles = "Admin")] → any other role gets redirected
//                                 to /Account/AccessDenied
//
//  Dashboard loads:
//    - 4 stat counts (Pending, InProgress, Completed, Available techs)
//    - Recent pending requests table (needs assignment)
//    - All technicians overview
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
    public class HomeController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<HomeController> _logger;
        private readonly IAutoAssignmentService _autoAssignment;

        public HomeController(IUnitOfWork uow, ILogger<HomeController> logger, IAutoAssignmentService autoAssignment)
        {
            _uow = uow;
            _logger = logger;
            _autoAssignment = autoAssignment;
        }

        // GET /Admin/Home/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            // ── Stat card counts ──────────────────────────────────
            // CountAsync translates to: SELECT COUNT(*) WHERE ...
            // Fast — no full rows loaded, just a number
            var pendingCount = await _uow.ServiceRequests
                .CountAsync(r => r.Status == RequestStatus.Pending);

            var inProgressCount = await _uow.ServiceRequests
                .CountAsync(r => r.Status == RequestStatus.InProgress);

            var completedCount = await _uow.ServiceRequests
                .CountAsync(r => r.Status == RequestStatus.Completed);

            var availableTechs = await _uow.TechnicianProfiles
                .CountAsync(t => t.Status == TechnicianStatus.Available);

            var totalTechs = await _uow.TechnicianProfiles
                .CountAsync();

            var totalCustomers = await _uow.Users
                .CountAsync(u => u.Role == UserRole.Customer);

            // ── Recent pending requests (need assignment) ─────────
            var pendingRequests = await _uow.ServiceRequests
                .GetByStatusAsync(RequestStatus.Pending);

            // ── Build the ViewModel ───────────────────────────────
            var vm = new AdminDashboardViewModel
            {
                PendingCount = pendingCount,
                InProgressCount = inProgressCount,
                CompletedCount = completedCount,
                AvailableTechs = availableTechs,
                TotalTechs = totalTechs,
                TotalCustomers = totalCustomers,
                PendingRequests = pendingRequests.Take(10).ToList()
            };

            return View(vm);
        }

        // POST /Admin/Home/RunAutoAssign
        // Admin can manually trigger one assignment cycle
        // Useful when they want immediate assignment without waiting 2 min
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RunAutoAssign()
        {
            _logger.LogInformation(
                "Manual auto-assignment triggered by admin {Admin}",
                User.Identity?.Name);

            await _autoAssignment.RunAsync();

            TempData["Success"] =
                "Auto-assignment cycle completed. " +
                "Check All Requests to see what was assigned.";

            return RedirectToAction(nameof(Dashboard));
        }

        // GET /Admin/Home/GetCounts
        // Returns live counts as JSON — called by SignalR client JS
        // when admin dashboard receives a StatusChanged notification
        [HttpGet]
        public async Task<IActionResult> GetCounts()
        {
            var pending = await _uow.ServiceRequests
                .CountAsync(r => r.Status == RequestStatus.Pending);
            var inProgress = await _uow.ServiceRequests
                .CountAsync(r => r.Status == RequestStatus.InProgress);
            var completed = await _uow.ServiceRequests
                .CountAsync(r => r.Status == RequestStatus.Completed);
            var available = await _uow.TechnicianProfiles
                .CountAsync(t => t.Status == TechnicianStatus.Available);

            return Json(new
            {
                pending,
                inProgress,
                completed,
                available
            });
        }
    }
}