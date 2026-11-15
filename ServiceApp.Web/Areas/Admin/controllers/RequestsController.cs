// =================================================================
//  Areas/Admin/Controllers/RequestsController.cs
//
//  Updated to support:
//  - Full-text search (customer name, description, address)
//  - Date range filter (today, this week, this month, custom)
//  - Status filter tabs
//  - Pagination (15 per page)
//  - CSV export (all matching results, no pagination limit)
//
//  GET  /Admin/Requests/Index          → list with search + filter
//  GET  /Admin/Requests/Details/{id}   → request detail
//  GET  /Admin/Requests/Assign/{id}    → assign technician
//  POST /Admin/Requests/Assign/{id}    → confirm assignment
//  POST /Admin/Requests/Cancel/{id}    → cancel request
//  GET  /Admin/Requests/ExportCsv      → download CSV
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Core.Models.Queries;
using ServiceApp.Web.Areas.Admin.Models;
using System.Text;

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

        // GET /Admin/Requests — search, status and date filters plus paging run in SQL.
        [HttpGet]
        public async Task<IActionResult> Index(
            string? search = null,
            string? status = null,
            string? dateRange = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int page = 1,
            [FromServices] IRequestQueries queries = null!)
        {
            var (from, to) = RequestSearchCriteria.ResolveDateRange(dateRange, dateFrom, dateTo);
            var vm = new RequestSearchViewModel
            {
                SearchTerm = search,
                StatusFilter = status,
                DateRange = dateRange,
                DateFrom = from,
                DateTo = to,
                CurrentPage = Math.Max(1, page)
            };

            var counts = await queries.CountByStatusAsync();
            vm.AllCount = counts.Values.Sum();
            vm.PendingCount = counts[RequestStatus.Pending];
            vm.AssignedCount = counts[RequestStatus.Assigned];
            vm.InProgressCount = counts[RequestStatus.InProgress];
            vm.BilledCount = counts[RequestStatus.Billed];
            vm.CompletedCount = counts[RequestStatus.Completed];
            vm.CancelledCount = counts[RequestStatus.Cancelled];

            var result = await queries.SearchAsync(BuildCriteria(search, status, from, to, vm.CurrentPage, vm.PageSize));
            vm.TotalCount = result.TotalCount;
            vm.Requests = result.Items.ToList();

            return View(vm);
        }

        // GET /Admin/Requests/ExportCsv — same filters as Index, every matching row.
        [HttpGet]
        public async Task<IActionResult> ExportCsv(
            string? search = null,
            string? status = null,
            string? dateRange = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            [FromServices] IRequestExportService export = null!)
        {
            var (from, to) = RequestSearchCriteria.ResolveDateRange(dateRange, dateFrom, dateTo);
            var (content, rows) = await export.ExportCsvAsync(BuildCriteria(search, status, from, to, 1, 0));

            _logger.LogInformation("CSV exported by admin {Admin}: {Count} records", User.Identity?.Name, rows);
            return File(content, "text/csv", $"ServiceApp-Requests-{DateTime.UtcNow:yyyyMMdd-HHmm}.csv");
        }

        private static RequestSearchCriteria BuildCriteria(
            string? search, string? status, DateTime? from, DateTime? to, int page, int pageSize) =>
            new()
            {
                Search = search,
                Statuses = Enum.TryParse<RequestStatus>(status, true, out var s) ? [s] : null,
                CreatedFrom = from,
                CreatedTo = to,
                Page = page,
                PageSize = pageSize
            };

        // =============================================================
        //  GET /Admin/Requests/Details/{id}
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
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Assign(int id)
        {
            var requestResult = await _requestService
                .GetRequestDetailsAsync(id);

            if (!requestResult.IsSuccess)
            {
                TempData["Error"] = requestResult.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            var request = requestResult.Data!;

            if (request.Status != RequestStatus.Pending)
            {
                TempData["Error"] =
                    $"Request #{id} is {request.Status} " +
                    "and cannot be assigned.";
                return RedirectToAction(nameof(Index));
            }

            var techResult = await _requestService
                .GetAvailableTechniciansAsync(request.Category);

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
                id,
                vm.SelectedTechnicianProfileId,
                adminUserId);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Assign), new { id });
            }

            TempData["Success"] =
                $"Technician assigned to request #{id}.";

            return RedirectToAction(
                nameof(Index),
                new { status = "Assigned" });
        }

        // =============================================================
        //  POST /Admin/Requests/Cancel/{id}
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

            TempData["Success"] = $"Request #{id} cancelled.";
            return RedirectToAction(nameof(Index));
        }

    }
}
