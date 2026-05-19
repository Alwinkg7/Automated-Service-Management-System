// =================================================================
//  Areas/Technician/Controllers/BillsController.cs
//
//  GET  /Technician/Bills/Create?requestId={id} → bill form
//  POST /Technician/Bills/Create                → submit bill
//  GET  /Technician/Bills/Details/{requestId}   → view bill
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;
using ServiceApp.Web.Areas.Technician.Models;

namespace ServiceApp.Web.Areas.Technician.Controllers
{
    [Area("Technician")]
    [Authorize(Roles = "Technician")]
    public class BillsController : Controller
    {
        private readonly IBillService _billService;
        private readonly IServiceRequestService _requestService;
        private readonly IUnitOfWork _uow;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BillsController> _logger;

        public BillsController(
            IBillService billService,
            IServiceRequestService requestService,
            IUnitOfWork uow,
            UserManager<ApplicationUser> userManager,
            ILogger<BillsController> logger)
        {
            _billService = billService;
            _requestService = requestService;
            _uow = uow;
            _userManager = userManager;
            _logger = logger;
        }

        // =============================================================
        //  GET /Technician/Bills/Create?requestId={id}
        //  Show the bill creation form.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Create(int requestId)
        {
            var requestResult = await _requestService
                .GetRequestDetailsAsync(requestId);

            if (!requestResult.IsSuccess)
            {
                TempData["Error"] = requestResult.Error;
                return RedirectToAction("Index", "Jobs");
            }

            var request = requestResult.Data!;

            // Verify this technician owns this job
            var userId = _userManager.GetUserId(User)!;
            var tech = await _uow.TechnicianProfiles
                .GetByUserIdAsync(userId);

            if (tech == null ||
                request.AssignedTechnicianProfileId
                    != tech.TechnicianProfileId)
            {
                TempData["Error"] =
                    "You can only create bills for your own jobs.";
                return RedirectToAction("Index", "Jobs");
            }

            // Already billed? Show existing bill
            if (request.Bill != null)
            {
                TempData["Error"] =
                    "A bill already exists for this request.";
                return RedirectToAction(nameof(Details),
                    new { requestId });
            }

            var vm = new CreateBillViewModel
            {
                Request = request,
                Items = new List<BillItemRow>
                {
                    new BillItemRow
                    {
                        Description = "Labour charges",
                        Quantity    = 1,
                        UnitPrice   = 0
                    }
                }
            };

            return View(vm);
        }

        // =============================================================
        //  POST /Technician/Bills/Create
        //  Submit the bill.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(
            CreateBillViewModel vm, int requestId)
        {
            // Re-load request for display if validation fails
            var requestResult = await _requestService
                .GetRequestDetailsAsync(requestId);

            if (!requestResult.IsSuccess)
            {
                TempData["Error"] = requestResult.Error;
                return RedirectToAction("Index", "Jobs");
            }

            vm.Request = requestResult.Data!;

            // Filter out completely blank rows before validating
            var filledItems = vm.Items?
                .Where(i => !string.IsNullOrWhiteSpace(i.Description)
                         && i.UnitPrice > 0)
                .ToList() ?? new List<BillItemRow>();

            if (!filledItems.Any())
            {
                ModelState.AddModelError(string.Empty,
                    "Please add at least one item with " +
                    "a description and price.");
                return View(vm);
            }

            var userId = _userManager.GetUserId(User)!;

            // Convert ViewModel rows to service layer DTOs
            var billItems = filledItems
                .Select(i => new BillItemInput
                {
                    Description = i.Description,
                    Quantity = i.Quantity > 0 ? i.Quantity : 1,
                    UnitPrice = i.UnitPrice
                })
                .ToList();

            var result = await _billService.CreateBillAsync(
                requestId, userId, billItems);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.Error!);
                return View(vm);
            }

            TempData["Success"] =
                $"Bill created successfully! " +
                $"Total: ₹{result.Data!.TotalAmount:N2}. " +
                "Waiting for customer payment.";

            return RedirectToAction(nameof(Details),
                new { requestId });
        }

        // =============================================================
        //  GET /Technician/Bills/Details/{requestId}
        //  View the bill for a completed or billed job.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Details(int requestId)
        {
            var result = await _billService
                .GetBillByRequestIdAsync(requestId);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.Error;
                return RedirectToAction("Index", "Jobs");
            }

            return View(result.Data!);
        }
    }
}