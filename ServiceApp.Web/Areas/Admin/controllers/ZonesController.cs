using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Web.Areas.Admin.Controllers
{
    /// <summary>Service-zone admin screens. Rules live in <see cref="IZoneService"/> (shared with the API).</summary>
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ZonesController : Controller
    {
        private readonly IZoneService _zones;

        public ZonesController(IZoneService zones) => _zones = zones;

        [HttpGet]
        public async Task<IActionResult> Index() =>
            View(await _zones.GetAllAsync(includeInactive: true));

        [HttpGet]
        public IActionResult Create() => View(new ServiceZone());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceZone vm)
        {
            var result = await _zones.CreateAsync(new ZoneInput(vm.ZoneName, vm.City, vm.State, vm.PinCodes));
            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage!);
                return View(vm);
            }

            TempData["Success"] =
                $"Zone '{result.Data!.ZoneName}' created with {result.Data.GetPinCodeList().Count} pin codes.";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var result = await _zones.GetAsync(id);
            if (result.IsSuccess) return View(result.Data);

            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ServiceZone vm)
        {
            var existing = await _zones.GetAsync(id);
            if (!existing.IsSuccess)
            {
                TempData["Error"] = existing.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            // The edit form has no active toggle: keep the current state.
            var result = await _zones.UpdateAsync(id,
                new ZoneInput(vm.ZoneName, vm.City, vm.State, vm.PinCodes, existing.Data!.IsActive));
            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage!);
                return View(vm);
            }

            TempData["Success"] = $"Zone '{result.Data!.ZoneName}' updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var result = await _zones.ToggleAsync(id);
            TempData[result.IsSuccess ? "Success" : "Error"] = result.IsSuccess
                ? $"Zone '{result.Data!.ZoneName}' is now {(result.Data.IsActive ? "active" : "inactive")}."
                : result.ErrorMessage;
            return RedirectToAction(nameof(Index));
        }
    }
}
