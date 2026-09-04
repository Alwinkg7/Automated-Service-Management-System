// =================================================================
//  Areas/Customer/Controllers/RequestsController.cs
//
//  Handles the customer-facing service request flow:
//
//  GET  /Customer/Requests/Create  → show booking form
//  POST /Customer/Requests/Create  → submit booking
//  GET  /Customer/Requests/Index   → my requests list
//  GET  /Customer/Requests/Details/{id} → request detail
//  POST /Customer/Requests/Cancel/{id}  → cancel a request
//
//  PATTERN:
//  Controller calls IServiceRequestService — never touches
//  IUnitOfWork or repositories directly.
//  Service returns Result<T> — controller checks IsSuccess.
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
        //  GET /Customer/Requests/Create
        //  Show the booking form.
        //  Pre-fill address + pin from their saved profile.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Create()
        {
            var userId = _userManager.GetUserId(User)!;

            // Load their saved profile to pre-fill address fields
            var profile = await _uow.CustomerProfiles
                .GetByUserIdAsync(userId);

            var vm = new CreateRequestViewModel
            {
                // Pre-fill from saved profile — customer can change it
                Address = profile?.Address ?? string.Empty,
                PinCode = profile?.PinCode ?? string.Empty,
                SavedAddress = profile?.Address,
                SavedPinCode = profile?.PinCode,

                // Pre-select their preferred category if set
                Category = profile?.PreferredCategory
                    ?? ServiceCategory.Electrical
            };

            return View(vm);
        }

        // =============================================================
        //  POST /Customer/Requests/Create
        //  Submit the booking form.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateRequestViewModel vm)
        {
            // Validate preferred time is at least 1 hour in future
            if (vm.PreferredDateTime <= DateTime.Now.AddHours(1))
            {
                ModelState.AddModelError(
                    nameof(vm.PreferredDateTime),
                    "Please select a time at least 1 hour from now.");
            }

            if (!ModelState.IsValid) return View(vm);

            var userId = _userManager.GetUserId(User)!;

            // Call service layer — all business logic lives there
            var result = await _requestService.CreateRequestAsync(
                customerId: userId,
                description: vm.Description,
                category: vm.Category,
                address: vm.Address,
                pinCode: vm.PinCode,
                preferredDateTime: vm.PreferredDateTime);

            if (!result.IsSuccess)
            {
                // Service returned a business error
                ModelState.AddModelError(string.Empty, result.ErrorMessage!);
                return View(vm);
            }

            TempData["Success"] =
                $"Your {vm.Category} request has been submitted! " +
                "We will assign a technician shortly.";

            // Go to My Requests to see the new request
            return RedirectToAction(nameof(Index));
        }

        // =============================================================
        //  GET /Customer/Requests/Index
        //  My Requests list — all requests for this customer.
        //  Optional filter by status via query string: ?filter=Pending
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Index(string? filter = null)
        {
            var userId = _userManager.GetUserId(User)!;

            var result = await _requestService
                .GetCustomerRequestsAsync(userId);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction("Dashboard", "Home");
            }

            // Parse optional status filter from query string
            RequestStatus? statusFilter = null;
            if (!string.IsNullOrEmpty(filter) &&
                Enum.TryParse<RequestStatus>(filter, out var parsed))
            {
                statusFilter = parsed;
            }

            var allRequests = result.Data!.ToList();

            // Apply filter if provided
            var filtered = statusFilter.HasValue
                ? allRequests.Where(r => r.Status == statusFilter.Value).ToList()
                : allRequests;

            var vm = new RequestListViewModel
            {
                AllRequests = filtered,
                CurrentFilter = statusFilter
            };

            return View(vm);
        }

        // =============================================================
        //  GET /Customer/Requests/Details/{id}
        //  Full detail of one request including timeline.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var result = await _requestService.GetRequestDetailsAsync(id);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            var request = result.Data!;

            // Security: customer can only see their own requests
            var userId = _userManager.GetUserId(User)!;
            if (request.CustomerId != userId)
            {
                TempData["Error"] = "You do not have access to this request.";
                return RedirectToAction(nameof(Index));
            }

            return View(request);
        }

        // =============================================================
        //  POST /Customer/Requests/Cancel/{id}
        //  Cancel a Pending or Assigned request.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Cancel(int id)
        {
            var userId = _userManager.GetUserId(User)!;

            // Verify ownership before cancelling
            var detailResult = await _requestService
                .GetRequestDetailsAsync(id);

            if (!detailResult.IsSuccess)
            {
                TempData["Error"] = detailResult.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            if (detailResult.Data!.CustomerId != userId)
            {
                TempData["Error"] =
                    "You can only cancel your own requests.";
                return RedirectToAction(nameof(Index));
            }

            var result = await _requestService
                .CancelRequestAsync(id, userId);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Details), new { id });
            }

            TempData["Success"] = "Request cancelled successfully.";
            return RedirectToAction(nameof(Index));
        }

        // POST Requests/SubmitRating/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SubmitRating(int id, int rating,
            string? feedback)
        {
            var userId = _userManager.GetUserId(User)!;

            var result = await _requestService.SubmitRatingAsync(
                id, userId, rating, feedback);

            if (!result.IsSuccess)
                TempData["Error"] = result.ErrorMessage;
            else
                TempData["Success"] = "Thank you for your rating!";

            return RedirectToAction(nameof(Details), new { id });
        }

        // =============================================================
        //  GET /Customer/Requests/History
        //  Full service history — completed + cancelled requests only.
        //  Separate from Index (which shows active + past mixed).
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> History(
            string? search = null,
            string? category = null)
        {
            var userId = _userManager.GetUserId(User)!;

            var result = await _requestService
                .GetCustomerRequestsAsync(userId);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(Index));
            }

            // Only show terminal states on history page
            var all = result.Data!
                .Where(r => r.Status == RequestStatus.Completed
                         || r.Status == RequestStatus.Cancelled)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();

            // Parse optional category filter
            ServiceCategory? catFilter = null;
            if (!string.IsNullOrEmpty(category) &&
                Enum.TryParse<ServiceCategory>(category, out var parsedCat))
                catFilter = parsedCat;

            // Apply filters
            var filtered = all.AsEnumerable();

            if (catFilter.HasValue)
                filtered = filtered.Where(r => r.Category == catFilter.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = search.Trim().ToLower();
                filtered = filtered.Where(r =>
                    r.Description.ToLower().Contains(term) ||
                    r.Address.ToLower().Contains(term) ||
                    (r.AssignedTechnician?.User?.FullName
                        .ToLower().Contains(term) ?? false));
            }

            var historyList = filtered.ToList();
            var completed = all.Where(r =>
                r.Status == RequestStatus.Completed).ToList();

            var vm = new ServiceHistoryViewModel
            {
                History = historyList,
                TotalJobs = all.Count,
                CompletedJobs = all.Count(r =>
                    r.Status == RequestStatus.Completed),
                CancelledJobs = all.Count(r =>
                    r.Status == RequestStatus.Cancelled),
                TotalSpent = completed
                    .Where(r => r.Bill != null)
                    .Sum(r => r.Bill!.TotalAmount),
                AverageRating = completed
                    .Where(r => r.CustomerRating.HasValue)
                    .Select(r => (double)r.CustomerRating!.Value)
                    .DefaultIfEmpty(0)
                    .Average(),
                CategoryFilter = catFilter,
                SearchTerm = search
            };

            // Pass category enum list to view for filter dropdown
            ViewBag.Categories = Enum.GetValues<ServiceCategory>()
                .Select(c => c.ToString())
                .ToList();

            return View(vm);
        }

        // =============================================================
        //  GET /Customer/Requests/Rebook/{id}
        //  Pre-fills the Create form with data from a past request.
        //  Customer can adjust anything before submitting.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Rebook(int id)
        {
            var userId = _userManager.GetUserId(User)!;

            var result = await _requestService.GetRequestDetailsAsync(id);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction(nameof(History));
            }

            var original = result.Data!;

            // Security: only the customer who made the original request
            if (original.CustomerId != userId)
            {
                TempData["Error"] =
                    "You can only re-book your own past requests.";
                return RedirectToAction(nameof(History));
            }

            // Only allow re-booking completed or cancelled requests
            if (original.Status != RequestStatus.Completed &&
                original.Status != RequestStatus.Cancelled)
            {
                TempData["Error"] =
                    "You can only re-book completed or cancelled requests.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var vm = new RebookViewModel
            {
                Category = original.Category,
                Description = original.Description,
                Address = original.Address,
                PinCode = original.PinCode,
                PreferredDateTime = DateTime.Now.AddHours(3),
                PreviousTechnicianName = original.AssignedTechnician
                    ?.User?.FullName,
                OriginalRequestId = id
            };

            return View(vm);
        }

        // =============================================================
        //  POST /Customer/Requests/Rebook
        //  Submit the re-booked request — same as Create but
        //  pre-filled. Creates a brand new ServiceRequest row.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Rebook(RebookViewModel vm)
        {
            // Validate time is at least 1 hour in future
            if (vm.PreferredDateTime <= DateTime.Now.AddHours(1))
            {
                ModelState.AddModelError(
                    nameof(vm.PreferredDateTime),
                    "Please select a time at least 1 hour from now.");
            }

            if (!ModelState.IsValid)
                return View(vm);

            var userId = _userManager.GetUserId(User)!;

            var result = await _requestService.CreateRequestAsync(
                customerId: userId,
                description: vm.Description,
                category: vm.Category,
                address: vm.Address,
                pinCode: vm.PinCode,
                preferredDateTime: vm.PreferredDateTime);

            if (!result.IsSuccess)
            {
                ModelState.AddModelError(string.Empty, result.ErrorMessage!);
                return View(vm);
            }

            TempData["Success"] =
                $"Re-booking confirmed! Your new {vm.Category} request " +
                $"#{result.Data!.RequestId} is Pending.";

            return RedirectToAction(nameof(Index));
        }
    }
}