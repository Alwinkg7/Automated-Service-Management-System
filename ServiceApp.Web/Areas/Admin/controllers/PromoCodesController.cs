using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PromoCodesController : Controller
    {
        private readonly IUnitOfWork _uow;

        public PromoCodesController(IUnitOfWork uow)
            => _uow = uow;

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var codes = (await _uow.PromoCodes.GetAllAsync())
                .OrderByDescending(p => p.CreatedAt)
                .ToList();
            return View(codes);
        }

        [HttpGet]
        public IActionResult Create()
            => View(new PromoCode
            {
                DiscountType = "Flat",
                MinimumAmount = 200,
                IsActive = true
            });

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PromoCode vm)
        {
            // Check unique code
            var existing = await _uow.PromoCodes
                .GetByCodeAsync(vm.Code);
            if (existing != null)
            {
                ModelState.AddModelError(
                    nameof(vm.Code),
                    "This code already exists.");
                return View(vm);
            }

            vm.Code = vm.Code.ToUpper().Trim();
            vm.UsedCount = 0;
            vm.CreatedAt = DateTime.UtcNow;

            await _uow.PromoCodes.AddAsync(vm);
            await _uow.SaveChangesAsync();

            TempData["Success"] =
                $"Promo code '{vm.Code}' created.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var code = await _uow.PromoCodes.GetByIdAsync(id);
            if (code == null)
            {
                TempData["Error"] = "Code not found.";
                return RedirectToAction(nameof(Index));
            }

            code.IsActive = !code.IsActive;
            _uow.PromoCodes.Update(code);
            await _uow.SaveChangesAsync();

            TempData["Success"] =
                $"Code '{code.Code}' is now " +
                $"{(code.IsActive ? "active" : "inactive")}.";
            return RedirectToAction(nameof(Index));
        }
    }
}