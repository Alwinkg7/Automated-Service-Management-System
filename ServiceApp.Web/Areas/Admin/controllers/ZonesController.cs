// =================================================================
//  ZonesController.cs
//
//  GET  /Admin/Zones/Index          → list all zones
//  GET  /Admin/Zones/Create         → create zone form
//  POST /Admin/Zones/Create         → save new zone
//  GET  /Admin/Zones/Edit/{id}      → edit zone form
//  POST /Admin/Zones/Edit/{id}      → save edits
//  POST /Admin/Zones/Toggle/{id}    → activate/deactivate
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ZonesController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<ZonesController> _logger;

        public ZonesController(IUnitOfWork uow,
            ILogger<ZonesController> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var zones = await _uow.ServiceZones.GetActiveZonesAsync();
            return View(zones);
        }

        [HttpGet]
        public IActionResult Create() => View(new ServiceZone());

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ServiceZone vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var zone = new ServiceZone
            {
                ZoneName = vm.ZoneName.Trim(),
                City = vm.City.Trim(),
                State = vm.State.Trim(),
                PinCodes = NormalizePinCodes(vm.PinCodes),
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _uow.ServiceZones.AddAsync(zone);
            await _uow.SaveChangesAsync();

            TempData["Success"] =
                $"Zone '{zone.ZoneName}' created with " +
                $"{zone.GetPinCodeList().Count} pin codes.";

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var zone = await _uow.ServiceZones.GetByIdAsync(id);
            if (zone == null)
            {
                TempData["Error"] = "Zone not found.";
                return RedirectToAction(nameof(Index));
            }
            return View(zone);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ServiceZone vm)
        {
            var zone = await _uow.ServiceZones.GetByIdAsync(id);
            if (zone == null)
            {
                TempData["Error"] = "Zone not found.";
                return RedirectToAction(nameof(Index));
            }

            zone.ZoneName = vm.ZoneName.Trim();
            zone.City = vm.City.Trim();
            zone.State = vm.State.Trim();
            zone.PinCodes = NormalizePinCodes(vm.PinCodes);

            _uow.ServiceZones.Update(zone);
            await _uow.SaveChangesAsync();

            TempData["Success"] = $"Zone '{zone.ZoneName}' updated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int id)
        {
            var zone = await _uow.ServiceZones.GetByIdAsync(id);
            if (zone == null)
            {
                TempData["Error"] = "Zone not found.";
                return RedirectToAction(nameof(Index));
            }

            zone.IsActive = !zone.IsActive;
            _uow.ServiceZones.Update(zone);
            await _uow.SaveChangesAsync();

            TempData["Success"] =
                $"Zone '{zone.ZoneName}' is now " +
                $"{(zone.IsActive ? "active" : "inactive")}.";

            return RedirectToAction(nameof(Index));
        }

        // Normalize pin codes — trim, remove duplicates, join with comma
        private static string NormalizePinCodes(string raw) =>
            string.Join(",",
                raw.Split(new[] { ',', '\n', '\r', ' ' },
                    StringSplitOptions.RemoveEmptyEntries)
                   .Select(p => p.Trim())
                   .Where(p => p.Length >= 4)
                   .Distinct());
    }
}