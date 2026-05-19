// =================================================================
//  Areas/Admin/Controllers/UsersController.cs
//
//  GET  /Admin/Users/Index          → all users list
//  GET  /Admin/Users/Create         → create admin form
//  POST /Admin/Users/Create         → submit new admin
//  POST /Admin/Users/Deactivate/{id}→ soft-delete a user
//
//  FLOW for creating a new admin:
//  1. Validate form
//  2. Check email not already taken
//  3. Create ApplicationUser via UserManager
//  4. Add to "Admin" role in Identity
//  5. Create AdminProfile row (with optional dept/designation)
//  6. Show success with login credentials
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
    public class UsersController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<UsersController> _logger;

        public UsersController(
            UserManager<ApplicationUser> userManager,
            IUnitOfWork uow,
            ILogger<UsersController> logger)
        {
            _userManager = userManager;
            _uow = uow;
            _logger = logger;
        }

        // =============================================================
        //  GET /Admin/Users/Index
        //  All registered users — filterable by role
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Index(string? filter = null)
        {
            // Parse role filter
            UserRole? roleFilter = null;
            if (!string.IsNullOrEmpty(filter) &&
                Enum.TryParse<UserRole>(filter, out var parsed))
                roleFilter = parsed;

            // Load users — filtered or all
            IEnumerable<ApplicationUser> users;
            if (roleFilter.HasValue)
                users = await _uow.Users.GetAllByRoleAsync(roleFilter.Value);
            else
                users = await _uow.Users.GetAllAsync();

            // Load counts for tab badges
            var allUsers = (await _uow.Users.GetAllAsync()).ToList();

            var vm = new UserListViewModel
            {
                AllUsers = users.OrderBy(u => u.FullName).ToList(),
                CurrentFilter = roleFilter,
                TotalCount = allUsers.Count,
                AdminCount = allUsers.Count(u =>
                    u.Role == UserRole.Admin),
                TechnicianCount = allUsers.Count(u =>
                    u.Role == UserRole.Technician),
                CustomerCount = allUsers.Count(u =>
                    u.Role == UserRole.Customer)
            };

            return View(vm);
        }

        // =============================================================
        //  GET /Admin/Users/Create
        //  Show the create admin form
        // =============================================================
        [HttpGet]
        public IActionResult Create() => View(new CreateAdminViewModel());

        // =============================================================
        //  POST /Admin/Users/Create
        //  Create the new admin account
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateAdminViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            // Check email not already taken
            var existing = await _userManager
                .FindByEmailAsync(vm.Email);
            if (existing != null)
            {
                ModelState.AddModelError(
                    nameof(vm.Email),
                    "This email is already registered.");
                return View(vm);
            }

            // ── Create ApplicationUser ─────────────────────────────
            var user = new ApplicationUser
            {
                UserName = vm.Email,
                Email = vm.Email,
                FullName = vm.FullName,
                Phone = vm.Phone,
                Role = UserRole.Admin,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager
                .CreateAsync(user, vm.Password);

            if (!result.Succeeded)
            {
                foreach (var err in result.Errors)
                    ModelState.AddModelError(
                        string.Empty, err.Description);
                return View(vm);
            }

            // ── Assign Admin role ──────────────────────────────────
            await _userManager.AddToRoleAsync(user, "Admin");

            // ── Create AdminProfile row ────────────────────────────
            try
            {
                await _uow.BeginTransactionAsync();

                var profile = new AdminProfile
                {
                    UserId = user.Id,
                    Department = vm.Department?.Trim(),
                    Designation = vm.Designation?.Trim(),
                    ProfileCompletedAt = (!string.IsNullOrEmpty(vm.Department)
                        || !string.IsNullOrEmpty(vm.Designation))
                        ? DateTime.UtcNow
                        : null
                };

                await _uow.AdminProfiles.AddAsync(profile);
                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "New admin created: {Email} by admin {CreatedBy}",
                    user.Email,
                    User.Identity?.Name);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                // Roll back the Identity user too
                await _userManager.DeleteAsync(user);
                _logger.LogError(ex,
                    "Failed to create admin profile for {Email}",
                    vm.Email);
                ModelState.AddModelError(string.Empty,
                    "Account creation failed. Please try again.");
                return View(vm);
            }

            TempData["Success"] =
                $"Admin account created for {vm.FullName}. " +
                $"They can login with: {vm.Email}";

            return RedirectToAction(nameof(Index),
                new { filter = "Admin" });
        }

        // =============================================================
        //  POST /Admin/Users/Deactivate/{id}
        //  Soft-delete — sets IsActive = false.
        //  User cannot login but data is preserved.
        //  Cannot deactivate yourself.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Deactivate(string id)
        {
            // Prevent self-deactivation
            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                TempData["Error"] =
                    "You cannot deactivate your own account.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            // Prevent deactivating the seeded system admin
            if (user.Email == "admin@serviceapp.com")
            {
                TempData["Error"] =
                    "The system admin account cannot be deactivated.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = false;
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                TempData["Error"] = "Failed to deactivate account.";
                return RedirectToAction(nameof(Index));
            }

            _logger.LogInformation(
                "User {Email} deactivated by admin {Admin}",
                user.Email, User.Identity?.Name);

            TempData["Success"] =
                $"{user.FullName}'s account has been deactivated.";
            return RedirectToAction(nameof(Index));
        }

        // =============================================================
        //  POST /Admin/Users/Reactivate/{id}
        //  Re-enable a deactivated account.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reactivate(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
            {
                TempData["Error"] = "User not found.";
                return RedirectToAction(nameof(Index));
            }

            user.IsActive = true;
            await _userManager.UpdateAsync(user);

            TempData["Success"] =
                $"{user.FullName}'s account has been reactivated.";
            return RedirectToAction(nameof(Index));
        }
    }
}