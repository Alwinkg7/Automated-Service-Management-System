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

        // =============================================================
        //  GET /Admin/Requests/Index
        //  All requests with search, date filter, status, pagination
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Index(
            string? search = null,
            string? status = null,
            string? dateRange = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            int page = 1)
        {
            // Load all requests with details (Customer + Technician)
            var all = (await _uow.ServiceRequests
                .GetAllWithDetailsAsync()).ToList();

            // ── Tab counts (always from unfiltered full list) ───────
            var vm = new RequestSearchViewModel
            {
                SearchTerm = search,
                StatusFilter = status,
                DateRange = dateRange,
                DateFrom = dateFrom,
                DateTo = dateTo,
                CurrentPage = Math.Max(1, page),

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

            // ── Apply filters sequentially ─────────────────────────

            var filtered = all.AsEnumerable();

            // 1. Search — customer name, description, address
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                filtered = filtered.Where(r =>
                    (r.Customer?.FullName.ToLower()
                        .Contains(term) ?? false) ||
                    r.Description.ToLower()
                        .Contains(term) ||
                    r.Address.ToLower()
                        .Contains(term) ||
                    r.PinCode.Contains(term) ||
                    r.RequestId.ToString() == term);
            }

            // 2. Status filter
            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<RequestStatus>(status, out var parsedStatus))
            {
                filtered = filtered.Where(r => r.Status == parsedStatus);
            }

            // 3. Date range filter
            (dateFrom, dateTo) = ResolveDateRange(
                dateRange, dateFrom, dateTo);

            if (dateFrom.HasValue)
                filtered = filtered.Where(r =>
                    r.CreatedAt.Date >= dateFrom.Value.Date);

            if (dateTo.HasValue)
                filtered = filtered.Where(r =>
                    r.CreatedAt.Date <= dateTo.Value.Date);

            // Sort: newest first
            var sorted = filtered
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            vm.TotalCount = sorted.Count;
            vm.DateFrom = dateFrom;
            vm.DateTo = dateTo;

            // 4. Paginate
            vm.Requests = sorted
                .Skip((vm.CurrentPage - 1) * vm.PageSize)
                .Take(vm.PageSize)
                .ToList();

            return View(vm);
        }

        // =============================================================
        //  GET /Admin/Requests/ExportCsv
        //  Downloads all matching results as a CSV file.
        //  Same filter params as Index — no pagination limit.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> ExportCsv(
            string? search = null,
            string? status = null,
            string? dateRange = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null)
        {
            var all = (await _uow.ServiceRequests
                .GetAllWithDetailsAsync()).ToList();

            var filtered = all.AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                filtered = filtered.Where(r =>
                    (r.Customer?.FullName.ToLower()
                        .Contains(term) ?? false) ||
                    r.Description.ToLower()
                        .Contains(term) ||
                    r.Address.ToLower().Contains(term));
            }

            if (!string.IsNullOrEmpty(status) &&
                Enum.TryParse<RequestStatus>(status, out var parsedStatus))
                filtered = filtered.Where(r => r.Status == parsedStatus);

            (dateFrom, dateTo) = ResolveDateRange(
                dateRange, dateFrom, dateTo);

            if (dateFrom.HasValue)
                filtered = filtered.Where(r =>
                    r.CreatedAt.Date >= dateFrom.Value.Date);

            if (dateTo.HasValue)
                filtered = filtered.Where(r =>
                    r.CreatedAt.Date <= dateTo.Value.Date);

            var results = filtered
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            // ── Build CSV ──────────────────────────────────────────
            var csv = new StringBuilder();

            // Header row
            csv.AppendLine(
                "Request ID,Customer,Phone,Category," +
                "Description,Address,Pin Code," +
                "Preferred Time,Technician,Status," +
                "Created At,Bill Amount,Payment Status");

            // Data rows
            foreach (var r in results)
            {
                csv.AppendLine(string.Join(",",
                    r.RequestId,
                    CsvEscape(r.Customer?.FullName ?? ""),
                    CsvEscape(r.Customer?.Phone ?? ""),
                    r.Category,
                    CsvEscape(r.Description),
                    CsvEscape(r.Address),
                    r.PinCode,
                    r.PreferredDateTime.ToString("dd MMM yyyy HH:mm"),
                    CsvEscape(r.AssignedTechnician?.User?.FullName ?? ""),
                    r.Status,
                    r.CreatedAt.ToString("dd MMM yyyy HH:mm"),
                    r.Bill?.TotalAmount.ToString("F2") ?? "",
                    r.Bill?.PaymentStatus.ToString() ?? ""
                ));
            }

            var bytes = Encoding.UTF8.GetBytes(csv.ToString());
            var fileName =
                $"ServiceApp-Requests-" +
                $"{DateTime.Now:yyyyMMdd-HHmm}.csv";

            _logger.LogInformation(
                "CSV exported by admin {Admin}: {Count} records",
                User.Identity?.Name, results.Count);

            // Return as downloadable file
            // UTF-8 BOM ensures Excel opens it correctly
            var bom = Encoding.UTF8.GetPreamble();
            var output = bom.Concat(bytes).ToArray();

            return File(output, "text/csv", fileName);
        }

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

        // =============================================================
        //  HELPER — resolve date range string to actual dates
        // =============================================================
        private static (DateTime? from, DateTime? to) ResolveDateRange(
            string? dateRange,
            DateTime? customFrom,
            DateTime? customTo)
        {
            var today = DateTime.UtcNow.Date;

            return dateRange switch
            {
                "today" => (today, today),
                "week" => (today.AddDays(-6), today),
                "month" => (today.AddDays(-29), today),
                "custom" => (customFrom, customTo),
                _ => (null, null)
            };
        }

        // Escape a value for CSV — wrap in quotes if it has comma/quote/newline
        private static string CsvEscape(string value)
        {
            if (value.Contains(',') ||
                value.Contains('"') ||
                value.Contains('\n'))
                return $"\"{value.Replace("\"", "\"\"")}\"";
            return value;
        }
    }
}