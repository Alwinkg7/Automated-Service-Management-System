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
    public class PromotionsController : Controller
    {
        private readonly IPromotionService _promotions;
        private readonly UserManager<ApplicationUser> _userManager;

        public PromotionsController(
            IPromotionService promotions,
            UserManager<ApplicationUser> userManager)
        {
            _promotions = promotions;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var userId = _userManager.GetUserId(User)!;
            var balance = await _promotions
                .GetPointsBalanceAsync(userId);
            var history = (await _promotions
                .GetPointsHistoryAsync(userId)).ToList();
            var refCode = await _promotions
                .GetOrCreateReferralCodeAsync(userId);

            var vm = new PromotionsViewModel
            {
                PointsBalance = balance,
                PointsValue = Math.Floor(balance / 100m) * 10m,
                ReferralCode = refCode,
                PointsHistory = history
            };

            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CheckPromo(
            string promoCode, decimal billAmount = 500)
        {
            var userId = _userManager.GetUserId(User)!;
            var result = await _promotions.ValidatePromoCodeAsync(
                promoCode, userId, billAmount);

            var vm = new PromotionsViewModel
            {
                PointsBalance = await _promotions
                    .GetPointsBalanceAsync(userId),
                ReferralCode = await _promotions
                    .GetOrCreateReferralCodeAsync(userId),
                PromoValid = result.IsSuccess,
                PromoDiscount = result.IsSuccess ? result.Data : 0,
                PromoMessage = result.IsSuccess
                    ? $"Valid! You save ₹{result.Data:N2} on your next booking."
                    : result.ErrorMessage
            };

            vm.PointsHistory = (await _promotions
                .GetPointsHistoryAsync(userId)).ToList();

            return View("Index", vm);
        }
    }
}