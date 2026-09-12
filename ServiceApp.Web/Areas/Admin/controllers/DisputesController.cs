using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Web.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DisputesController : Controller
    {
        private readonly IDisputeService _disputes;
        private readonly ILogger<DisputesController> _logger;

        public DisputesController(
            IDisputeService disputes,
            ILogger<DisputesController> logger)
        {
            _disputes = disputes;
            _logger = logger;
        }

        // GET /Admin/Disputes/Index
        [HttpGet]
        public async Task<IActionResult> Index(string? status = null)
        {
            var all = await _disputes.GetAllDisputesAsync(status);

            ViewBag.CurrentFilter = status;
            ViewBag.OpenCount = (await _disputes
                .GetAllDisputesAsync("Open")).Count();
            ViewBag.ReviewCount = (await _disputes
                .GetAllDisputesAsync("UnderReview")).Count();
            ViewBag.ResolvedCount = (await _disputes
                .GetAllDisputesAsync("Resolved")).Count();
            ViewBag.SlaBreached = (await _disputes
                .GetSlaBreachedDisputesAsync()).Count();

            return View(all);
        }

        // GET /Admin/Disputes/Details/{id}
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var dispute = await _disputes.GetDisputeDetailsAsync(id);
            if (dispute == null)
            {
                TempData["Error"] = "Dispute not found.";
                return RedirectToAction(nameof(Index));
            }
            return View(dispute);
        }

        // POST /Admin/Disputes/StartReview
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartReview(
            int id, string adminNote)
        {
            var result = await _disputes.StartReviewAsync(id, adminNote);
            TempData[result.IsSuccess ? "Success" : "Error"] =
                result.IsSuccess
                    ? $"Dispute #{id} is now under review."
                    : result.ErrorMessage;

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Admin/Disputes/Resolve
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Resolve(
            int id, string resolution, string adminNote)
        {
            var result = await _disputes
                .ResolveAsync(id, resolution, adminNote);

            TempData[result.IsSuccess ? "Success" : "Error"] =
                result.IsSuccess
                    ? $"Dispute #{id} resolved as {resolution}."
                    : result.ErrorMessage;

            return RedirectToAction(nameof(Details), new { id });
        }

        // POST /Admin/Disputes/IssueRefund
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> IssueRefund(
            int id, decimal refundAmount, string adminNote)
        {
            var result = await _disputes
                .IssueRefundAsync(id, refundAmount, adminNote);

            TempData[result.IsSuccess ? "Success" : "Error"] =
                result.IsSuccess
                    ? $"Refund of ₹{refundAmount:N2} issued " +
                      $"via Razorpay. Ref: {result.Data!.RazorpayRefundId}"
                    : result.ErrorMessage;

            return RedirectToAction(nameof(Details), new { id });
        }
    }
}