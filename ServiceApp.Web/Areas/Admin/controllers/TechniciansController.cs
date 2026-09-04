// =================================================================
//  TechniciansController.cs
//
//  GET  /Admin/Technicians/Index          → all technicians
//  GET  /Admin/Technicians/Details/{id}   → one technician + jobs
//  POST /Admin/Technicians/OverrideStatus → force status change
//  POST /Admin/Technicians/Deactivate/{id}→ deactivate account
//  POST /Admin/Technicians/Reactivate/{id}→ reactivate account
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Web.Areas.Admin.Models;

namespace ServiceApp.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class TechniciansController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TechniciansController> _logger;

        public TechniciansController(
            IUnitOfWork uow,
            UserManager<ApplicationUser> userManager,
            ILogger<TechniciansController> logger)
        {
            _uow = uow;
            _userManager = userManager;
            _logger = logger;
        }

        // =============================================================
        //  GET /Admin/Technicians/Index
        //  All technicians with optional skill + status filters.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? skill = null,
            string? status = null)
        {
            // Load all technicians with user info
            var allTechs = (await _uow.TechnicianProfiles
                .GetAllWithUsersAsync()).ToList();

            // Load all requests to calculate earnings per tech
            var allRequests = (await _uow.ServiceRequests
                .GetAllWithDetailsAsync()).ToList();

            // Parse filters
            ServiceCategory? skillFilter = null;
            TechnicianStatus? statusFilter = null;

            if (!string.IsNullOrEmpty(skill) &&
                Enum.TryParse<ServiceCategory>(skill, out var parsedSkill))
                skillFilter = parsedSkill;

            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<TechnicianStatus>(status, out var parsedStatus))
                statusFilter = parsedStatus;

            // Build row items
            var rows = allTechs.Select(t => BuildRowItem(t, allRequests)).ToList();

            // Counts for tab badges (always based on full list)
            var vm = new TechnicianListViewModel
            {
                SkillFilter = skill,
                StatusFilter = statusFilter,
                TotalCount = rows.Count,
                AvailableCount = rows.Count(r =>
                    r.Status == TechnicianStatus.Available),
                BusyCount = rows.Count(r =>
                    r.Status == TechnicianStatus.Busy),
                OfflineCount = rows.Count(r =>
                    r.Status == TechnicianStatus.Offline)
            };

            // Apply filters
            var filtered = rows.AsEnumerable();
            if (skillFilter.HasValue)
                filtered = filtered.Where(r => r.Skill == skillFilter.Value);
            if (statusFilter.HasValue)
                filtered = filtered.Where(r => r.Status == statusFilter.Value);

            vm.Technicians = filtered
                .OrderByDescending(r => r.TotalJobsCompleted)
                .ToList();

            // Pass skill enum values to view for filter dropdown
            ViewBag.Skills = Enum.GetValues<ServiceCategory>()
                .Select(s => s.ToString())
                .ToList();

            return View(vm);
        }

        // =============================================================
        //  GET /Admin/Technicians/Details/{id}
        //  One technician's profile + full job history + earnings chart.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var tech = await _uow.TechnicianProfiles
                .GetWithUserAsync(id);

            if (tech == null)
            {
                TempData["Error"] = "Technician not found.";
                return RedirectToAction(nameof(Index));
            }

            // Load all their jobs
            var jobs = (await _uow.ServiceRequests
                .GetByTechnicianIdAsync(id)).ToList();

            // Earnings per month for last 6 months
            var months = new List<string>();
            var earnings = new List<decimal>();

            for (int i = 5; i >= 0; i--)
            {
                var month = DateTime.UtcNow.AddMonths(-i);
                var monthLabel = month.ToString("MMM yyyy");
                var monthEarned = jobs
                    .Where(j => j.Bill != null
                             && j.Bill.PaymentStatus == PaymentStatus.Paid
                             && j.Bill.PaidAt.HasValue
                             && j.Bill.PaidAt.Value.Year == month.Year
                             && j.Bill.PaidAt.Value.Month == month.Month)
                    .Sum(j => j.Bill!.TotalAmount);

                months.Add(monthLabel);
                earnings.Add(monthEarned);
            }

            // Total earnings
            var totalEarned = jobs
                .Where(j => j.Bill != null
                         && j.Bill.PaymentStatus == PaymentStatus.Paid)
                .Sum(j => j.Bill!.TotalAmount);

            // Get all requests for earnings calc
            var allRequests = (await _uow.ServiceRequests
                .GetAllWithDetailsAsync()).ToList();

            var vm = new TechnicianDetailViewModel
            {
                Profile = BuildRowItem(tech, allRequests),
                JobHistory = jobs
                    .OrderByDescending(j => j.CreatedAt)
                    .ToList(),
                EarningsMonths = months,
                EarningsValues = earnings
            };

            return View(vm);
        }

        // =============================================================
        //  POST /Admin/Technicians/OverrideStatus
        //  Admin forces a technician's status change.
        //  Use case: tech is stuck as Busy after a cancelled job.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OverrideStatus(
            int id, string newStatus)
        {
            var tech = await _uow.TechnicianProfiles.GetByIdAsync(id);
            if (tech == null)
            {
                TempData["Error"] = "Technician not found.";
                return RedirectToAction(nameof(Index));
            }

            if (!Enum.TryParse<TechnicianStatus>(newStatus, out var parsed))
            {
                TempData["Error"] = "Invalid status.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var oldStatus = tech.Status;
            tech.Status = parsed;
            _uow.TechnicianProfiles.Update(tech);
            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Admin {Admin} overrode technician #{TechId} " +
                "status from {Old} to {New}",
                User.Identity?.Name, id, oldStatus, parsed);

            TempData["Success"] =
                $"Status updated from {oldStatus} to {parsed}.";

            return RedirectToAction(nameof(Details), new { id });
        }

        // =============================================================
        //  POST /Admin/Technicians/Deactivate/{id}
        //  Soft-delete — technician cannot log in.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(int id)
        {
            var tech = await _uow.TechnicianProfiles
                .GetWithUserAsync(id);

            if (tech == null)
            {
                TempData["Error"] = "Technician not found.";
                return RedirectToAction(nameof(Index));
            }

            tech.User.IsActive = false;
            await _userManager.UpdateAsync(tech.User);

            TempData["Success"] =
                $"{tech.User.FullName} has been deactivated.";

            return RedirectToAction(nameof(Index));
        }

        // =============================================================
        //  POST /Admin/Technicians/Reactivate/{id}
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(int id)
        {
            var tech = await _uow.TechnicianProfiles
                .GetWithUserAsync(id);

            if (tech == null)
            {
                TempData["Error"] = "Technician not found.";
                return RedirectToAction(nameof(Index));
            }

            tech.User.IsActive = true;
            await _userManager.UpdateAsync(tech.User);

            TempData["Success"] =
                $"{tech.User.FullName} has been reactivated.";

            return RedirectToAction(nameof(Index));
        }

        // =============================================================
        //  HELPER — build a flat TechnicianRowItem from the entity
        // =============================================================
        private static TechnicianRowItem BuildRowItem(
            TechnicianProfile tech,
            List<ServiceApp.Core.Entities.ServiceRequest> allRequests)
        {
            var techJobs = allRequests
                .Where(r => r.AssignedTechnicianProfileId
                         == tech.TechnicianProfileId)
                .ToList();

            var totalEarned = techJobs
                .Where(j => j.Bill != null
                         && j.Bill.PaymentStatus == PaymentStatus.Paid)
                .Sum(j => j.Bill!.TotalAmount);

            var activeJobs = techJobs.Count(j =>
                j.Status == RequestStatus.InProgress ||
                j.Status == RequestStatus.Assigned);

            return new TechnicianRowItem
            {
                TechnicianProfileId = tech.TechnicianProfileId,
                UserId = tech.UserId,
                FullName = tech.User.FullName,
                Email = tech.User.Email ?? "",
                Phone = tech.User.Phone,
                Skill = tech.Skill,
                Status = tech.Status,
                Rating = tech.Rating,
                TotalJobsCompleted = tech.TotalJobsCompleted,
                TotalEarned = totalEarned,
                Bio = tech.Bio,
                YearsOfExperience = tech.YearsOfExperience,
                ServiceAreaPinCode = tech.ServiceAreaPinCode,
                IsActive = tech.User.IsActive,
                ActiveJobCount = activeJobs
            };
        }
    }
}