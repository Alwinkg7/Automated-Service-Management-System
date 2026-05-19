// =================================================================
//  Areas/Technician/Controllers/ProfileController.cs
//
//  GET  /Technician/Profile/Index → show profile form
//  POST /Technician/Profile/Index → save profile data
//  POST /Technician/Profile/ToggleStatus → Available ↔ Offline
//
//  ToggleStatus is called from the Dashboard's "Go offline"
//  / "Go online" button. Only works when not Busy (on a job).
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
    public class ProfileController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<ProfileController> _logger;

        public ProfileController(
            IUnitOfWork uow,
            UserManager<ApplicationUser> userManager,
            ILogger<ProfileController> logger)
        {
            _uow = uow;
            _userManager = userManager;
            _logger = logger;
        }

        // GET /Technician/Profile/Index
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login",
                "Account", new { area = "" });

            var profile = await _uow.TechnicianProfiles
                .GetByUserIdAsync(user.Id);

            if (profile == null) return RedirectToAction("Login",
                "Account", new { area = "" });

            var vm = new TechnicianProfileViewModel
            {
                // Read-only fields
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Phone = user.Phone,
                Skill = profile.Skill,
                Rating = profile.Rating,
                TotalJobsCompleted = profile.TotalJobsCompleted,

                // Editable fields
                Bio = profile.Bio ?? string.Empty,
                YearsOfExperience = profile.YearsOfExperience,
                ServiceAreaPinCode = profile.ServiceAreaPinCode
                    ?? string.Empty,
                AvatarUrl = profile.AvatarUrl,

                IsExistingProfile = !string.IsNullOrEmpty(profile.Bio)
            };

            return View(vm);
        }

        // POST /Technician/Profile/Index
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(TechnicianProfileViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                // Re-populate read-only fields
                var currentUser = await _userManager.GetUserAsync(User);
                var currentProfile = await _uow.TechnicianProfiles
                    .GetByUserIdAsync(currentUser!.Id);

                vm.FullName = currentUser.FullName;
                vm.Email = currentUser.Email ?? string.Empty;
                vm.Phone = currentUser.Phone;
                vm.Skill = currentProfile!.Skill;
                vm.Rating = currentProfile.Rating;
                vm.TotalJobsCompleted = currentProfile.TotalJobsCompleted;

                return View(vm);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login",
                "Account", new { area = "" });

            var profile = await _uow.TechnicianProfiles
                .GetByUserIdAsync(user.Id);

            if (profile == null) return RedirectToAction("Login",
                "Account", new { area = "" });

            // Update profile fields
            profile.Bio = vm.Bio.Trim();
            profile.YearsOfExperience = vm.YearsOfExperience;
            profile.ServiceAreaPinCode = vm.ServiceAreaPinCode.Trim();
            profile.AvatarUrl = vm.AvatarUrl?.Trim();
            profile.ProfileCompletedAt = DateTime.UtcNow;

            _uow.TechnicianProfiles.Update(profile);
            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Technician profile updated for user {UserId}", user.Id);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Dashboard", "Home");
        }

        // POST /Technician/Profile/ToggleStatus
        // Called from Dashboard — switches Available ↔ Offline
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login",
                "Account", new { area = "" });

            var profile = await _uow.TechnicianProfiles
                .GetByUserIdAsync(user.Id);

            if (profile == null)
                return RedirectToAction("Dashboard", "Home");

            // Only allow toggle when NOT Busy (on an active job)
            // Busy status is managed by the job lifecycle — not manually
            if (profile.Status == TechnicianStatus.Busy)
            {
                TempData["Error"] =
                    "Cannot change status while on an active job.";
                return RedirectToAction("Dashboard", "Home");
            }

            // Toggle: Available → Offline or Offline → Available
            profile.Status = profile.Status == TechnicianStatus.Available
                ? TechnicianStatus.Offline
                : TechnicianStatus.Available;

            _uow.TechnicianProfiles.Update(profile);
            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Technician {UserId} toggled status to {Status}",
                user.Id, profile.Status);

            TempData["Success"] =
                $"Status updated to {profile.Status}.";
            return RedirectToAction("Dashboard", "Home");
        }
    }
}