// =================================================================
//  Areas/Admin/Controllers/RequestsController.cs
//
//  GET  /Admin/Requests/Index          → all requests + filters
//  GET  /Admin/Requests/Details/{id}   → request detail view
//  GET  /Admin/Requests/Assign/{id}    → assign technician screen
//  POST /Admin/Requests/Assign/{id}    → confirm assignment
//  POST /Admin/Requests/Cancel/{id}    → admin cancels a request
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
    public class RequestsController : Controller
    {
        private readonly IServiceRequestService _requestService;
        private readonly IUnitOfWork _uow;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<RequestsController> _logger;

        public RequestsController(
            IServiceRequestService requestService,
            IUnitOfWork uow,
            UserManager<ApplicationUser> userManager,
            ILogger<RequestsController> logger)
        {
            _requestService = requestService;
            _uow = uow;
            _userManager = userManager;
            _logger = logger;
        }

        // =============================================================
        //  GET /Admin/Requests/Index
        //  All requests across all customers.
        //  Optional filter: ?filter=Pending
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Index(string? filter = null)
        {
            // Parse filter
            RequestStatus? statusFilter = null;
            if (!string.IsNullOrEmpty(filter) &&
                Enum.TryParse<RequestStatus>(filter, out var parsed))
                statusFilter = parsed;

            // Load requests — filtered or all
            var result = await _requestService
                .GetAllRequestsAsync(statusFilter);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction("Dashboard", "Home");
            }

            // Load all for counts (tab badges always show full counts)
            var allResult = await _requestService.GetAllRequestsAsync();
            var all = allResult.Data?.ToList() ?? new List<ServiceRequest>();

            var vm = new AdminRequestListViewModel
            {
                Requests = result.Data!.ToList(),
                CurrentFilter = statusFilter,

                // Count badges — always based on full list
                AllCount = all.Count,
                PendingCount = all.Count(r =>
                    r.Status == RequestStatus.Pending),
                AssignedCount = all.Count(r =>
                    r.Status == RequestStatus.Assigned),
                InProgressCount = all.Count(r =>
                    r.Status == RequestStatus.InProgress),
                BilledCount = all.Count(r =>
                    r.Status == RequestStatus.Billed),
                CompletedCount = all.Count(r =>
                    r.Status == RequestStatus.Completed),
                CancelledCount = all.Count(r =>
                    r.Status == RequestStatus.Cancelled)
            };

            return View(vm);
        }

        // =============================================================
        //  GET /Admin/Requests/Details/{id}
        //  Full detail view — same as customer but with admin actions
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var result = await _requestService
                .GetRequestDetailsAsync(id);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            return View(result.Data!);
        }

        // =============================================================
        //  GET /Admin/Requests/Assign/{id}
        //  Show the assign screen:
        //    - Request details on the left
        //    - Available matching technicians on the right
        //
        //  Only Pending requests can be assigned.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Assign(int id)
        {
            // Load the request
            var requestResult = await _requestService
                .GetRequestDetailsAsync(id);

            if (!requestResult.IsSuccess)
            {
                TempData["Error"] = requestResult.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            var request = requestResult.Data!;

            // Only Pending requests can be assigned
            if (request.Status != RequestStatus.Pending)
            {
                TempData["Error"] =
                    $"Request #{id} is {request.Status} " +
                    "and cannot be assigned. " +
                    "Only Pending requests can be assigned.";
                return RedirectToAction(nameof(Index));
            }

            // Find available technicians matching the category
            var techResult = await _requestService
                .GetAvailableTechniciansAsync(request.Category);

            // Not a hard error if none found — show empty state
            var technicians = techResult.IsSuccess
                ? techResult.Data!.ToList()
                : new List<TechnicianProfile>();

            var vm = new AssignTechnicianViewModel
            {
                Request = request,
                AvailableTechnicians = technicians
            };

            return View(vm);
        }

        // =============================================================
        //  POST /Admin/Requests/Assign/{id}
        //  Admin confirms the assignment.
        //  Calls service layer which validates + transitions status.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Assign(
            int id, AssignTechnicianViewModel vm)
        {
            if (vm.SelectedTechnicianProfileId == 0)
            {
                TempData["Error"] =
                    "Please select a technician before assigning.";
                return RedirectToAction(nameof(Assign), new { id });
            }

            var adminUserId = _userManager.GetUserId(User)!;

            var result = await _requestService.AssignTechnicianAsync(
                requestId: id,
                technicianProfileId: vm.SelectedTechnicianProfileId,
                adminUserId: adminUserId);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Assign), new { id });
            }

            TempData["Success"] =
                $"Technician assigned to request #{id} successfully.";

            return RedirectToAction(nameof(Index),
                new { filter = "Assigned" });
        }

        // =============================================================
        //  POST /Admin/Requests/Cancel/{id}
        //  Admin cancels a Pending or Assigned request.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var adminUserId = _userManager.GetUserId(User)!;

            var result = await _requestService
                .CancelRequestAsync(id, adminUserId);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Success"] =
                $"Request #{id} has been cancelled.";
            return RedirectToAction(nameof(Index));
        }
    }
}