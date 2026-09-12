using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Web.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize(Roles = "Customer")]
    public class DisputesController : Controller
    {
        private readonly IDisputeService _disputes;
        private readonly IUnitOfWork _uow;
        private readonly UserManager<ApplicationUser> _userManager;

        public DisputesController(
            IDisputeService disputes,
            IUnitOfWork uow,
            UserManager<ApplicationUser> userManager)
        {
            _disputes = disputes;
            _uow = uow;
            _userManager = userManager;
        }

        // GET /Customer/Disputes/Raise/{requestId}
        [HttpGet]
        public async Task<IActionResult> Raise(int requestId)
        {
            var userId = _userManager.GetUserId(User)!;
            var request = await _uow.ServiceRequests
                .GetWithDetailsAsync(requestId);

            if (request == null || request.CustomerId != userId)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction("Index", "Requests");
            }

            // Check existing dispute
            var existing = await _disputes
                .GetDisputeByRequestIdAsync(requestId);
            if (existing != null && existing.Status != "Resolved")
            {
                TempData["Error"] =
                    $"A dispute already exists for this request " +
                    $"(#{existing.DisputeId} — {existing.Status}).";
                return RedirectToAction("Details", "Requests",
                    new { id = requestId });
            }

            ViewBag.Request = request;
            return View();
        }

        // POST /Customer/Disputes/Raise
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Raise(
            int requestId, string reason, string description)
        {
            if (string.IsNullOrWhiteSpace(description) ||
                description.Length < 20)
            {
                TempData["Error"] =
                    "Please provide at least 20 characters " +
                    "describing the issue.";
                return RedirectToAction(nameof(Raise),
                    new { requestId });
            }

            var userId = _userManager.GetUserId(User)!;

            var result = await _disputes.RaiseDisputeAsync(
                requestId, userId, reason, description);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction("Details", "Requests",
                    new { id = requestId });
            }

            TempData["Success"] =
                $"Dispute #{result.Data!.DisputeId} raised. " +
                "Admin will review within 48 hours.";

            return RedirectToAction(nameof(MyDisputes));
        }

        // GET /Customer/Disputes/MyDisputes
        [HttpGet]
        public async Task<IActionResult> MyDisputes()
        {
            var userId = _userManager.GetUserId(User)!;
            var all = await _disputes.GetAllDisputesAsync();
            var myDisputes = all
                .Where(d => d.RaisedByUserId == userId)
                .OrderByDescending(d => d.RaisedAt)
                .ToList();

            return View(myDisputes);
        }

        // GET /Customer/Disputes/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var userId = _userManager.GetUserId(User)!;
            var dispute = await _disputes.GetDisputeDetailsAsync(id);

            if (dispute == null ||
                dispute.RaisedByUserId != userId)
            {
                TempData["Error"] = "Dispute not found.";
                return RedirectToAction(nameof(MyDisputes));
            }

            return View(dispute);
        }
    }
}