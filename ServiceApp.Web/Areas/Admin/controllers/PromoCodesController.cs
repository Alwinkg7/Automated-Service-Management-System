using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Web.Areas.Admin.Controllers
{
    /// <summary>Promo-code admin screens. Rules live in <see cref="IPromoCodeService"/> (shared with the API).</summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PromoCodesController : Controller
    {
        private readonly IPromoCodeService _codes;

        public PromoCodesController(IPromoCodeService codes) => _codes = codes;

        [HttpGet]
        public async Task<IActionResult> Index() => View(await _codes.GetAllAsync());

        [HttpGet]
        public IActionResult Create() =>
            View(new PromoCode { DiscountType = "Flat", MinimumAmount = 200, IsActive = true });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PromoCode vm)
        {
            var result = await _codes.CreateAsync(new PromoCodeInput(
                vm.Code, vm.Description, vm.DiscountType, vm.DiscountValue, vm.MaxDiscount,
                vm.MinimumAmount, vm.MaxUses, vm.ExpiresAt, vm.IsActive));

            if (!result.IsSuccess)
            {
                ModelState.AddModelError(nameof(vm.Code), result.ErrorMessage!);
                return View(vm);
            }

            TempData["Success"] = $"Promo code '{result.Data!.Code}' created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var result = await _codes.ToggleAsync(id);
            TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
                ? $"Code '{result.Data!.Code}' is now {(result.Data.IsActive ? "active" : "inactive")}."
                : result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }
    }
}
