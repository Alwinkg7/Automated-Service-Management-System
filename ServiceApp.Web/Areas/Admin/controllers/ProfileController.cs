// =================================================================
//  Areas/Admin/Controllers/ProfileController.cs
//
//  GET  /Admin/Profile/Index → show admin profile form
//  POST /Admin/Profile/Index → save admin profile
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;
using ServiceApp.Web.Areas.Admin.Models;

namespace ServiceApp.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
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

        // GET /Admin/Profile/Index
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login",
                "Account", new { area = "" });

            var profile = await _uow.AdminProfiles
                .GetByUserIdAsync(user.Id);

            var vm = new AdminProfileViewModel
            {
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Phone = user.Phone,
                Department = profile?.Department,
                Designation = profile?.Designation,
                EmployeeId = profile?.EmployeeId,
                AvatarUrl = profile?.AvatarUrl,
                IsExistingProfile = profile != null
            };

            return View(vm);
        }

        // POST /Admin/Profile/Index
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(AdminProfileViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                var currentUser = await _userManager.GetUserAsync(User);
                vm.FullName = currentUser?.FullName ?? string.Empty;
                vm.Email = currentUser?.Email ?? string.Empty;
                vm.Phone = currentUser?.Phone ?? string.Empty;
                return View(vm);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login",
                "Account", new { area = "" });

            var profile = await _uow.AdminProfiles
                .GetByUserIdAsync(user.Id);

            if (profile == null)
            {
                profile = new AdminProfile { UserId = user.Id };
                await _uow.AdminProfiles.AddAsync(profile);
            }

            profile.Department = vm.Department?.Trim();
            profile.Designation = vm.Designation?.Trim();
            profile.EmployeeId = vm.EmployeeId?.Trim();
            profile.AvatarUrl = vm.AvatarUrl?.Trim();
            profile.ProfileCompletedAt = DateTime.UtcNow;

            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Admin profile updated for user {UserId}", user.Id);

            TempData["Success"] = "Profile updated successfully!";
            return RedirectToAction("Dashboard", "Home");
        }
    }
}