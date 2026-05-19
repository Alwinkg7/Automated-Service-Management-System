// =================================================================
//  Areas/Technician/Controllers/JobsController.cs
//
//  Handles all technician job actions:
//
//  GET  /Technician/Jobs/Index        → all my jobs (list)
//  GET  /Technician/Jobs/Details/{id} → full job detail + timeline
//  POST /Technician/Jobs/Accept/{id}  → accept assigned job
//  POST /Technician/Jobs/Reject/{id}  → reject assigned job
//
//  Every action first verifies the logged-in technician
//  is actually the one assigned to the request.
//  This prevents one technician from acting on another's job.
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Web.Areas.Technician.Models;

namespace ServiceApp.Web.Areas.Technician.Controllers
{
    [Area("Technician")]
    [Authorize(Roles = "Technician")]
    public class JobsController : Controller
    {
        private readonly IServiceRequestService _requestService;
        private readonly IUnitOfWork _uow;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<JobsController> _logger;

        public JobsController(
            IServiceRequestService requestService,
            IUnitOfWork uow,
            UserManager<ApplicationUser> userManager,
            ILogger<JobsController> logger)
        {
            _requestService = requestService;
            _uow = uow;
            _userManager = userManager;
            _logger = logger;
        }

        // =============================================================
        //  HELPER — get the logged-in technician's profile
        //  Returns null if not found (safety check).
        //  Used by every action in this controller.
        // =============================================================
        private async Task<TechnicianProfile?> GetTechProfileAsync()
        {
            var userId = _userManager.GetUserId(User)!;
            return await _uow.TechnicianProfiles.GetByUserIdAsync(userId);
        }

        // =============================================================
        //  GET /Technician/Jobs/Index
        //  All jobs assigned to this technician — grouped by active/past
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Index(string? filter = null)
        {
            var tech = await GetTechProfileAsync();
            if (tech == null)
            {
                TempData["Error"] = "Technician profile not found.";
                return RedirectToAction("Dashboard", "Home");
            }

            var result = await _requestService
                .GetTechnicianJobsAsync(tech.TechnicianProfileId);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction("Dashboard", "Home");
            }

            // Parse optional status filter
            RequestStatus? statusFilter = null;
            if (!string.IsNullOrEmpty(filter) &&
                Enum.TryParse<RequestStatus>(filter, out var parsed))
                statusFilter = parsed;

            var jobs = result.Data!.ToList();

            // Apply filter if provided
            var filtered = statusFilter.HasValue
                ? jobs.Where(j => j.Status == statusFilter.Value).ToList()
                : jobs;

            // Pass technician info to view
            ViewBag.TechProfile = tech;
            ViewBag.CurrentFilter = statusFilter;

            // Count badges
            ViewBag.AssignedCount = jobs.Count(j =>
                j.Status == RequestStatus.Assigned);
            ViewBag.InProgressCount = jobs.Count(j =>
                j.Status == RequestStatus.InProgress);
            ViewBag.CompletedCount = jobs.Count(j =>
                j.Status == RequestStatus.Completed);

            return View(filtered);
        }

        // =============================================================
        //  GET /Technician/Jobs/Details/{id}
        //  Full job detail with customer info, timeline, bill status
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var tech = await GetTechProfileAsync();
            if (tech == null)
            {
                TempData["Error"] = "Technician profile not found.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _requestService.GetRequestDetailsAsync(id);
            if (!result.IsSuccess)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Index));
            }

            var request = result.Data!;

            // Technician can only view jobs assigned to them
            if (request.AssignedTechnicianProfileId
                != tech.TechnicianProfileId)
            {
                TempData["Error"] =
                    "You do not have access to this job.";
                return RedirectToAction(nameof(Index));
            }

            var vm = new JobDetailViewModel
            {
                Request = request,
                TechnicianProfile = tech
            };

            return View(vm);
        }

        // =============================================================
        //  POST /Technician/Jobs/Accept/{id}
        //  Technician accepts an Assigned job.
        //
        //  What happens (in one transaction in the service layer):
        //    1. Request.Status → InProgress
        //    2. TechnicianProfile.Status → Busy
        //    3. ServiceHistory row inserted
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(int id)
        {
            var userId = _userManager.GetUserId(User)!;

            var result = await _requestService
                .AcceptJobAsync(id, userId);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Success"] =
                "Job accepted! You are now marked as Busy. " +
                "Head to the customer's address and get started.";

            return RedirectToAction(nameof(Details), new { id });
        }

        // =============================================================
        //  POST /Technician/Jobs/Reject/{id}
        //  Technician rejects an Assigned job.
        //
        //  What happens:
        //    1. Request.Status → Pending (back to queue)
        //    2. AssignedTechnicianProfileId cleared
        //    3. ServiceHistory row inserted
        //    4. Admin must reassign
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int id)
        {
            var userId = _userManager.GetUserId(User)!;

            var result = await _requestService
                .RejectJobAsync(id, userId);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Success"] =
                "Job rejected. The request has been returned " +
                "to the queue for reassignment.";

            // Go back to job list — this job no longer belongs to them
            return RedirectToAction(nameof(Index));
        }
    }
}