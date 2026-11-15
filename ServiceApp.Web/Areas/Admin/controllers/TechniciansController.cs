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
using ServiceApp.Core.Models.Queries;
using ServiceApp.Core.Models.Reports;
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

        // GET /Admin/Technicians — filters and earnings computed in SQL.
        [HttpGet]
        public async Task<IActionResult> Index(
            string? skill = null,
            string? status = null,
            [FromServices] ITechnicianQueries queries = null!)
        {
            ServiceCategory? skillFilter = Enum.TryParse<ServiceCategory>(skill, true, out var s) ? s : null;
            TechnicianStatus? statusFilter = Enum.TryParse<TechnicianStatus>(status, true, out var st) ? st : null;

            var counts = await queries.CountByStatusAsync();
            var page = await queries.SearchAsync(new TechnicianSearchCriteria
            {
                Skill = skillFilter,
                Status = statusFilter,
                PageSize = 0
            });

            var vm = new TechnicianListViewModel
            {
                SkillFilter = skill,
                StatusFilter = statusFilter,
                TotalCount = counts.Total,
                AvailableCount = counts.Available,
                BusyCount = counts.Busy,
                OfflineCount = counts.Offline,
                Technicians = page.Items.Select(ToRow).ToList()
            };

            ViewBag.Skills = Enum.GetValues<ServiceCategory>().Select(x => x.ToString()).ToList();
            return View(vm);
        }

        // GET /Admin/Technicians/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(int id, [FromServices] ITechnicianQueries queries = null!)
        {
            var summary = await queries.GetSummaryAsync(id);
            if (summary == null)
            {
                TempData["Error"] = "Technician not found.";
                return RedirectToAction(nameof(Index));
            }

            var jobs = await _uow.ServiceRequests.GetByTechnicianIdAsync(id);
            var earnings = await queries.GetMonthlyEarningsAsync(id, 6);

            return View(new TechnicianDetailViewModel
            {
                Profile = ToRow(summary),
                JobHistory = jobs.OrderByDescending(j => j.CreatedAt).ToList(),
                EarningsMonths = earnings.Select(e => e.Label).ToList(),
                EarningsValues = earnings.Select(e => e.Amount).ToList()
            });
        }

        // POST /Admin/Technicians/OverrideStatus/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> OverrideStatus(int id, string newStatus, [FromServices] ITechnicianService technicians = null!)
        {
            if (!Enum.TryParse<TechnicianStatus>(newStatus, true, out var parsed))
            {
                TempData["Error"] = "Invalid status.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var result = await technicians.OverrideStatusAsync(id, parsed);
            if (result.IsSuccess)
            {
                _logger.LogInformation("Admin {Admin} set technician #{TechId} status to {New}", User.Identity?.Name, id, parsed);
                TempData["Success"] = $"Status updated to {parsed}.";
            }
            else
            {
                TempData["Error"] = result.ErrorMessage;
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // =============================================================
        //  POST /Admin/Technicians/Deactivate/{id}
        //  Soft-delete — technician cannot log in.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Deactivate(int id, [FromServices] IAccountService accounts) =>
            SetActiveAsync(id, false, accounts, "deactivated");

        // =============================================================
        //  POST /Admin/Technicians/Reactivate/{id}
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> Reactivate(int id, [FromServices] IAccountService accounts) =>
            SetActiveAsync(id, true, accounts, "reactivated");

        private async Task<IActionResult> SetActiveAsync(int technicianProfileId, bool active, IAccountService accounts, string verb)
        {
            var tech = await _uow.TechnicianProfiles.GetWithUserAsync(technicianProfileId);
            if (tech == null)
            {
                TempData["Error"] = "Technician not found.";
                return RedirectToAction(nameof(Index));
            }

            var result = await accounts.SetActiveAsync(tech.UserId, active, _userManager.GetUserId(User)!);
            if (result.IsSuccess)
                TempData["Success"] = $"{tech.User.FullName} has been {verb}.";
            else
                TempData["Error"] = result.ErrorMessage;

            return RedirectToAction(nameof(Index));
        }

        private static TechnicianRowItem ToRow(TechnicianSummary t) => new()
        {
            TechnicianProfileId = t.TechnicianProfileId,
            UserId = t.UserId,
            FullName = t.FullName,
            Email = t.Email,
            Phone = t.Phone,
            Skill = t.Skill,
            Status = t.Status,
            Rating = t.Rating,
            TotalJobsCompleted = t.TotalJobsCompleted,
            TotalEarned = t.TotalEarned,
            Bio = t.Bio,
            YearsOfExperience = t.YearsOfExperience,
            ServiceAreaPinCode = t.ServiceAreaPinCode,
            IsActive = t.IsActive,
            ActiveJobCount = t.ActiveJobCount
        };
    }
}
