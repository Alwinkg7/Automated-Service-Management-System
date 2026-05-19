// =================================================================
//  AccountController.cs — ServiceApp.Web/Controllers
//
//  Handles all authentication flows:
//    GET/POST  /Account/CustomerRegister
//    GET/POST  /Account/TechnicianRegister
//    GET/POST  /Account/Login
//    POST      /Account/Logout
//    GET       /Account/AccessDenied
//
//  REGISTRATION FLOW (both roles):
//  1. Validate the form (ModelState)
//  2. Check email not already taken
//  3. Create ApplicationUser via UserManager (hashes password)
//  4. Add to Identity role (what [Authorize] checks)
//  5. Create the profile row (CustomerProfile or TechnicianProfile)
//     — wrapped in a transaction so both succeed or both fail
//  6. Sign the user in (set auth cookie)
//  7. Redirect to their dashboard
//
//  LOGIN FLOW:
//  1. Validate form
//  2. PasswordSignInAsync — checks hash, handles lockout
//  3. Read Role from ApplicationUser
//  4. Redirect to correct dashboard based on role
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Web.ViewModels.Auth;

namespace ServiceApp.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IUnitOfWork _uow;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IUnitOfWork uow,
            ILogger<AccountController> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _uow = uow;
            _logger = logger;
        }

        // =============================================================
        //  CUSTOMER REGISTER
        // =============================================================

        // GET /Account/CustomerRegister
        [HttpGet]
        [AllowAnonymous]
        public IActionResult CustomerRegister()
        {
            // If already logged in, send to their dashboard
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToDashboard();

            return View();
        }

        // POST /Account/CustomerRegister
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CustomerRegister(
            CustomerRegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            // Check email not already taken before creating user
            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(nameof(model.Email),
                    "This email is already registered. Please login instead.");
                return View(model);
            }

            // ── Create the ApplicationUser ────────────────────────
            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Phone = model.Phone,
                Role = UserRole.Customer,
                EmailConfirmed = true, // skip email verification for now
                IsActive = true
            };

            // UserManager handles password hashing automatically
            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            // ── Assign Identity role ──────────────────────────────
            // This is what [Authorize(Roles = "Customer")] checks
            await _userManager.AddToRoleAsync(user, "Customer");

            // ── Create CustomerProfile row ────────────────────────
            // Use a transaction: if profile creation fails,
            // we don't want a user with no profile in the DB
            try
            {
                await _uow.BeginTransactionAsync();

                var profile = new CustomerProfile
                {
                    UserId = user.Id  // FK link to ApplicationUser
                    // Address, City etc. filled later on profile page
                };

                await _uow.CustomerProfiles.AddAsync(profile);
                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "New customer registered: {Email}", user.Email);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                // Delete the Identity user we just created
                // so the signup can be retried cleanly
                await _userManager.DeleteAsync(user);
                _logger.LogError(ex,
                    "Failed to create customer profile for {Email}", user.Email);
                ModelState.AddModelError(string.Empty,
                    "Registration failed. Please try again.");
                return View(model);
            }

            // ── Auto sign-in after registration ───────────────────
            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["Success"] =
                $"Welcome, {user.FullName}! Complete your profile to get started.";

            return RedirectToDashboard();
        }

        // =============================================================
        //  TECHNICIAN REGISTER
        // =============================================================

        // GET /Account/TechnicianRegister
        [HttpGet]
        [AllowAnonymous]
        public IActionResult TechnicianRegister()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToDashboard();

            return View();
        }

        // POST /Account/TechnicianRegister
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TechnicianRegister(
            TechnicianRegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var existingUser = await _userManager.FindByEmailAsync(model.Email);
            if (existingUser != null)
            {
                ModelState.AddModelError(nameof(model.Email),
                    "This email is already registered. Please login instead.");
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.Email,
                Email = model.Email,
                FullName = model.FullName,
                Phone = model.Phone,
                Role = UserRole.Technician,
                EmailConfirmed = true,
                IsActive = true
            };

            var result = await _userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, "Technician");

            // ── Create TechnicianProfile row ──────────────────────
            // Skill is captured at signup — critical for assignment
            try
            {
                await _uow.BeginTransactionAsync();

                var profile = new TechnicianProfile
                {
                    UserId = user.Id,
                    Skill = model.Skill,
                    Status = TechnicianStatus.Available
                    // Bio, Experience etc. filled on profile page
                };

                await _uow.TechnicianProfiles.AddAsync(profile);
                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "New technician registered: {Email} Skill: {Skill}",
                    user.Email, model.Skill);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                await _userManager.DeleteAsync(user);
                _logger.LogError(ex,
                    "Failed to create technician profile for {Email}", user.Email);
                ModelState.AddModelError(string.Empty,
                    "Registration failed. Please try again.");
                return View(model);
            }

            await _signInManager.SignInAsync(user, isPersistent: false);
            TempData["Success"] =
                $"Welcome, {user.FullName}! Complete your profile to start receiving jobs.";

            return RedirectToDashboard();
        }

        // =============================================================
        //  LOGIN — single page for all roles
        // =============================================================

        // GET /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToDashboard();

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            // PasswordSignInAsync:
            // - Checks password hash
            // - Handles lockout tracking automatically
            // - lockoutOnFailure: true → counts bad attempts
            var result = await _signInManager.PasswordSignInAsync(
                model.Email,
                model.Password,
                model.RememberMe,
                lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in: {Email}", model.Email);

                // Return to the page they were trying to visit
                // IsLocalUrl prevents open redirect attacks
                if (!string.IsNullOrEmpty(returnUrl)
                    && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                return RedirectToDashboard();
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Account locked: {Email}", model.Email);
                ModelState.AddModelError(string.Empty,
                    "Too many failed attempts. Account locked for 5 minutes.");
                return View(model);
            }

            // Don't say "wrong password" specifically
            // That leaks info about whether the email exists
            ModelState.AddModelError(string.Empty,
                "Invalid email or password.");
            return View(model);
        }

        // =============================================================
        //  LOGOUT
        // =============================================================

        // POST only — GET logout allows CSRF attacks via image tags
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out.");
            return RedirectToAction("Login");
        }

        // =============================================================
        //  ACCESS DENIED
        // =============================================================

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        // =============================================================
        //  HELPER — redirect each role to their own dashboard
        // =============================================================

        // User.IsInRole() reads from the auth cookie claims
        // — no database hit required
        private IActionResult RedirectToDashboard()
        {
            if (User.IsInRole("Admin"))
                return RedirectToAction("Dashboard", "Home",
                    new { area = "Admin" });

            if (User.IsInRole("Technician"))
                return RedirectToAction("Dashboard", "Home",
                    new { area = "Technician" });

            return RedirectToAction("Dashboard", "Home",
                new { area = "Customer" });
        }
    }
}