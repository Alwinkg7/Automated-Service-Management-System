// =================================================================
//  Areas/Customer/Controllers/ProfileController.cs
//
//  GET  /Customer/Profile/Index → show profile form
//  POST /Customer/Profile/Index → save profile data
//
//  LOGIC:
//  1. Load the CustomerProfile row for this user (may be empty)
//  2. If it exists → pre-fill the form (edit mode)
//  3. If not → show empty form (first-time setup)
//  4. On POST → INSERT or UPDATE the CustomerProfiles row
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;
using ServiceApp.Web.Areas.Customer.Models;

namespace ServiceApp.Web.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize(Roles = "Customer")]
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

        // GET /Customer/Profile/Index
        public async Task<IActionResult> Index()
        {
            // Step 1: get the logged-in user's full record
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login",
                "Account", new { area = "" });

            // Step 2: try to load their existing profile row
            var profile = await _uow.CustomerProfiles
                .GetByUserIdAsync(user.Id);

            // Step 3: build the ViewModel
            // Pre-fill with existing data if profile exists
            var vm = new CustomerProfileViewModel
            {
                // Display fields from User table (read-only)
                FullName = user.FullName,
                Email = user.Email ?? string.Empty,
                Phone = user.Phone,

                // Editable fields from CustomerProfiles table
                // Use empty string/null if no profile yet
                Address = profile?.Address ?? string.Empty,
                City = profile?.City ?? string.Empty,
                PinCode = profile?.PinCode ?? string.Empty,
                PreferredCategory = profile?.PreferredCategory,
                AvatarUrl = profile?.AvatarUrl,

                // Tells the view whether to show "Save profile"
                // or "Update profile" on the button
                IsExistingProfile = profile != null
                    && !string.IsNullOrEmpty(profile.Address)
            };

            return View(vm);
        }

        // POST /Customer/Profile/Index
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Index(CustomerProfileViewModel vm)
        {
            if (!ModelState.IsValid)
            {
                // Re-populate read-only fields before returning view
                // (they're not posted back by the form)
                var currentUser = await _userManager.GetUserAsync(User);
                if (currentUser != null)
                {
                    vm.FullName = currentUser.FullName;
                    vm.Email = currentUser.Email ?? string.Empty;
                    vm.Phone = currentUser.Phone;
                }
                return View(vm);
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return RedirectToAction("Login",
                "Account", new { area = "" });

            // Check if a profile row already exists
            var profile = await _uow.CustomerProfiles
                .GetByUserIdAsync(user.Id);

            if (profile == null)
            {
                // First time — INSERT a new row
                profile = new CustomerProfile
                {
                    UserId = user.Id
                };
                await _uow.CustomerProfiles.AddAsync(profile);
            }

            // Map ViewModel fields → entity fields
            // (Same whether INSERT or UPDATE — EF handles both)
            profile.Address = vm.Address.Trim();
            profile.City = vm.City.Trim();
            profile.PinCode = vm.PinCode.Trim();
            profile.PreferredCategory = vm.PreferredCategory;
            profile.AvatarUrl = vm.AvatarUrl?.Trim();
            profile.ProfileCompletedAt = DateTime.UtcNow;

            await _uow.SaveChangesAsync();

            _logger.LogInformation(
                "Customer profile updated for user {UserId}", user.Id);

            TempData["Success"] = "Profile saved successfully!";
            return RedirectToAction("Dashboard", "Home");
        }
    }
}