// =================================================================
//  Areas/Customer/Controllers/HomeController.cs
//
//  Customer dashboard loads:
//    - Their recent service requests (last 5)
//    - Quick stats: total, pending, completed
//    - Profile completion banner if profile is incomplete
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Web.Areas.Customer.Models;

namespace ServiceApp.Web.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize(Roles = "Customer")]
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

        // GET /Customer/Home/Dashboard
        public async Task<IActionResult> Dashboard()
        {
            // GetUserId reads from the auth cookie — no DB hit
            var userId = _userManager.GetUserId(User)!;

            // Load this customer's requests (newest first)
            var requests = await _uow.ServiceRequests
                .GetByCustomerIdAsync(userId);

            var requestList = requests.ToList();

            // Load profile to check if it's been completed
            // Shows "Complete your profile" banner if not
            var profile = await _uow.CustomerProfiles
                .GetByUserIdAsync(userId);

            var vm = new CustomerDashboardViewModel
            {
                // Quick stats
                TotalRequests = requestList.Count,
                PendingCount = requestList.Count(r =>
                    r.Status == RequestStatus.Pending),
                InProgressCount = requestList.Count(r =>
                    r.Status == RequestStatus.InProgress),
                CompletedCount = requestList.Count(r =>
                    r.Status == RequestStatus.Completed),

                // Show only the 5 most recent on dashboard
                RecentRequests = requestList.Take(5).ToList(),

                // Profile completion check
                // Profile row exists but Address is null = incomplete
                IsProfileComplete = profile != null
                    && !string.IsNullOrEmpty(profile.Address)
            };

            return View(vm);
        }
    }
}