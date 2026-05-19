// =================================================================
//  Areas/Technician/Controllers/HomeController.cs
//
//  Technician dashboard loads:
//    - Their current status (Available/Busy/Offline)
//    - Active jobs (Assigned or InProgress) — need action
//    - Stats: total jobs, completed, rating
//    - Profile completion banner if incomplete
// =================================================================

using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Web.Areas.Technician.Models;
using Microsoft.AspNetCore.Authentication;

namespace ServiceApp.Web.Areas.Technician.Controllers
{
    [Area("Technician")]
    [Authorize(Roles = "Technician")]
    public class HomeController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly UserManager<ApplicationUser> _userManager;

        public HomeController(
            IUnitOfWork uow,
            UserManager<ApplicationUser> userManager)
        {
            _uow = uow;
            _userManager = userManager;
        }

        // GET /Technician/Home/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            var userId = _userManager.GetUserId(User)!;

            // Load the technician profile row linked to this user
            var profile = await _uow.TechnicianProfiles
                .GetByUserIdAsync(userId);

            // Safety: if profile not found, log out and start over
            // This shouldn't happen normally
            if (profile == null)
            {
                await HttpContext.SignOutAsync();
                return RedirectToAction("Login", "Account",
                    new { area = "" });
            }

            // Load all jobs for this technician
            var jobs = await _uow.ServiceRequests
                .GetByTechnicianIdAsync(profile.TechnicianProfileId);

            var jobList = jobs.ToList();

            // Split into active (need action) and past (read-only)
            var activeJobs = jobList
                .Where(j => j.Status == RequestStatus.Assigned
                         || j.Status == RequestStatus.InProgress)
                .OrderBy(j => j.CreatedAt)
                .ToList();

            var recentPastJobs = jobList
                .Where(j => j.Status == RequestStatus.Completed
                         || j.Status == RequestStatus.Cancelled
                         || j.Status == RequestStatus.Billed)
                .OrderByDescending(j => j.UpdatedAt)
                .Take(5)
                .ToList();

            var vm = new TechnicianDashboardViewModel
            {
                TechnicianProfileId = profile.TechnicianProfileId,
                CurrentStatus = profile.Status,
                Skill = profile.Skill,
                Rating = profile.Rating,
                TotalJobsCompleted = profile.TotalJobsCompleted,

                // Active jobs shown at the top — these need attention
                ActiveJobs = activeJobs,

                // Recent past jobs shown below
                RecentPastJobs = recentPastJobs,

                // Stats
                TotalAssigned = jobList.Count,
                CompletedCount = jobList.Count(j =>
                    j.Status == RequestStatus.Completed),

                // Profile completion check
                IsProfileComplete = !string.IsNullOrEmpty(profile.Bio)
            };

            return View(vm);
        }
    }
}