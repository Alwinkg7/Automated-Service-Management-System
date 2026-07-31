// =================================================================
//  Areas/Customer/Controllers/BillsController.cs
//
//  GET  /Customer/Bills/Pay?requestId={id} → payment page
//  POST /Customer/Bills/PayCash            → process cash payment
//  GET  /Customer/Bills/Confirmation/{id}  → success page
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Web.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize(Roles = "Customer")]
    public class BillsController : Controller
    {
        private readonly IPaymentService _paymentService;
        private readonly IBillService _billService;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<BillsController> _logger;
        private readonly IConfiguration _configuration;

        public BillsController(
            IPaymentService paymentService,
            IBillService billService,
            UserManager<ApplicationUser> userManager,
            ILogger<BillsController> logger,
            IConfiguration configuration)
        {
            _paymentService = paymentService;
            _billService = billService;
            _userManager = userManager;
            _logger = logger;
            _configuration = configuration;
        }

        // =============================================================
        //  GET /Customer/Bills/Pay?requestId={id}
        //  Show the payment page with bill summary.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Pay(int requestId)
        {
            var userId = _userManager.GetUserId(User)!;

            var result = await _paymentService
                .GetBillForPaymentAsync(requestId, userId);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction("Index", "Requests");
            }

            // Pass public KeyId to view — safe to expose in HTML
            ViewBag.RazorpayKeyId = _configuration["Razorpay:KeyId"];

            return View(result.Data!);
        }

        // =============================================================
        //  POST /Customer/Bills/PayCash
        //  Customer selects Cash — process immediately.
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayCash(int billId)
        {
            var userId = _userManager.GetUserId(User)!;

            var result = await _paymentService
                .ProcessCashPaymentAsync(billId, userId);

            if (!result.IsSuccess)
            {
                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction("Index", "Requests");
            }

            TempData["Success"] =
                "Payment confirmed! Thank you for using ServiceApp.";

            return RedirectToAction(
                nameof(Confirmation),
                new { paymentId = result.Data!.PaymentId });
        }

        // =============================================================
        //  GET /Customer/Bills/Confirmation/{paymentId}
        //  Payment success page.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Confirmation(int paymentId)
        {
            var payment = await _paymentService
                .GetPaymentByIdAsync(paymentId);

            if (payment == null)
                return RedirectToAction("Index", "Requests");

            return View(payment);
        }
    }
}