using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;
using ServiceApp.Core.Models.Accounts;
using ServiceApp.Web.ViewModels.Auth;

namespace ServiceApp.Web.Controllers
{
    /// <summary>
    /// Cookie-based registration, login and logout for the MVC site. Account
    /// creation is delegated to <see cref="IAccountService"/> (shared with the API).
    /// </summary>
    public class AccountController : Controller
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAccountService _accounts;
        private readonly ILogger<AccountController> _logger;

        public AccountController(
            SignInManager<ApplicationUser> signInManager,
            IAccountService accounts,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _accounts = accounts;
            _logger = logger;
        }

        // ── Customer registration ──

        [HttpGet]
        [AllowAnonymous]
        public IActionResult CustomerRegister()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToDashboard();

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CustomerRegister(CustomerRegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var referralCode = Request.Cookies["referral_code"] ?? model.ReferralCode;
            var result = await _accounts.RegisterCustomerAsync(new CustomerRegistration(
                model.FullName, model.Email, model.Phone, model.Password, referralCode));

            if (!result.IsSuccess)
                return RegistrationFailed(model, nameof(model.Email), result.ErrorType, result.ErrorMessage);

            await _signInManager.SignInAsync(result.Data!, isPersistent: false);
            TempData["Success"] = $"Welcome, {result.Data!.FullName}! Complete your profile to get started.";

            return RedirectToRoleDashboard("Customer");
        }

        // ── Technician registration ──

        [HttpGet]
        [AllowAnonymous]
        public IActionResult TechnicianRegister()
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToDashboard();

            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TechnicianRegister(TechnicianRegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var result = await _accounts.RegisterTechnicianAsync(new TechnicianRegistration(
                model.FullName, model.Email, model.Phone, model.Password, model.Skill));

            if (!result.IsSuccess)
                return RegistrationFailed(model, nameof(model.Email), result.ErrorType, result.ErrorMessage);

            await _signInManager.SignInAsync(result.Data!, isPersistent: false);
            TempData["Success"] = $"Welcome, {result.Data!.FullName}! Complete your profile to start receiving jobs.";

            return RedirectToRoleDashboard("Technician");
        }

        // ── Login / logout ──

        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
                return RedirectToDashboard();

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            // Deactivated accounts cannot sign in (same generic message: no account enumeration).
            var account = await _signInManager.UserManager.FindByEmailAsync(model.Email);
            if (account is { IsActive: false })
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(
                model.Email, model.Password, model.RememberMe, lockoutOnFailure: true);

            if (result.Succeeded)
            {
                _logger.LogInformation("User logged in via MVC.");

                // IsLocalUrl prevents open redirects.
                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return Redirect(returnUrl);

                var user = await _signInManager.UserManager.FindByEmailAsync(model.Email);
                return RedirectToRoleDashboard(user?.Role.ToString());
            }

            if (result.IsLockedOut)
            {
                _logger.LogWarning("Account locked out after failed MVC logins.");
                ModelState.AddModelError(string.Empty, "Too many failed attempts. Account locked for 5 minutes.");
                return View(model);
            }

            ModelState.AddModelError(string.Empty, "Invalid email or password.");
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Login");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult AccessDenied() => View();

        // ── Helpers ──

        private IActionResult RegistrationFailed(object model, string emailField, ErrorType errorType, string? message)
        {
            ModelState.AddModelError(
                errorType == ErrorType.Conflict ? emailField : string.Empty,
                message ?? "Registration failed. Please try again.");
            return View(model);
        }

        private IActionResult RedirectToDashboard() =>
            RedirectToRoleDashboard(
                User.IsInRole("Admin") ? "Admin" :
                User.IsInRole("Technician") ? "Technician" : "Customer");

        // The freshly issued cookie is not on the current request yet, so after
        // sign-in the role must be passed explicitly rather than read from User.
        private IActionResult RedirectToRoleDashboard(string? role) => role switch
        {
            "Admin" => RedirectToAction("Dashboard", "Home", new { area = "Admin" }),
            "Technician" => RedirectToAction("Dashboard", "Home", new { area = "Technician" }),
            _ => RedirectToAction("Dashboard", "Home", new { area = "Customer" })
        };
    }
}
