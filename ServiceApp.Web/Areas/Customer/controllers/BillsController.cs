// =================================================================
//  Areas/Customer/Controllers/BillsController.cs
//
//  All payment actions for the customer.
//
//  GET  /Customer/Bills/Pay?requestId={id}       → payment page
//  POST /Customer/Bills/PayCash                   → cash confirm
//  GET  /Customer/Bills/InitiateRazorpay?billId={id} → create order JSON
//  POST /Customer/Bills/ProcessRazorpay           → verify + complete
//  GET  /Customer/Bills/Confirmation?paymentId={id}  → receipt page
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
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<BillsController> _logger;

        public BillsController(
            IPaymentService paymentService,
            UserManager<ApplicationUser> userManager,
            IConfiguration configuration,
            ILogger<BillsController> logger)
        {
            _paymentService = paymentService;
            _userManager = userManager;
            _configuration = configuration;
            _logger = logger;
        }

        // =============================================================
        //  GET /Customer/Bills/Pay?requestId={id}
        //
        //  Loads the bill for this request and shows payment options.
        //  Sets ViewBag.RazorpayKeyId so the JS checkout can use it.
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

            // Public key — safe to put in HTML/JS
            ViewBag.RazorpayKeyId = _configuration["Razorpay:KeyId"];

            return View(result.Data!);
        }

        // =============================================================
        //  POST /Customer/Bills/PayCash
        //  No gateway — marks bill as paid with Cash method instantly.
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
                "Cash payment confirmed! Request is now complete.";

            return RedirectToAction(
                nameof(Confirmation),
                new { paymentId = result.Data!.PaymentId });
        }

        // =============================================================
        //  GET /Customer/Bills/InitiateRazorpay?billId={id}
        //
        //  Called via fetch() from Pay.cshtml JavaScript.
        //  Creates an order on Razorpay server, returns JSON.
        //
        //  The JS needs the Razorpay order_id before it can open
        //  the checkout modal. We create it here server-side so
        //  the KeySecret never touches the browser.
        //
        //  Response: { success, orderId, amount (paise), currency }
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> InitiateRazorpay(int billId)
        {
            var userId = _userManager.GetUserId(User)!;

            var result = await _paymentService
                .CreateRazorpayOrderAsync(billId, userId);

            if (!result.IsSuccess)
            {
                _logger.LogWarning(
                    "Razorpay order failed for bill #{BillId}: {Error}",
                    billId, result.ErrorMessage);

                return Json(new
                {
                    success = false,
                    error = result.ErrorMessage
                });
            }

            return Json(new
            {
                success = true,
                orderId = result.Data!.OrderId,
                amount = result.Data.Amount,    // in paise
                currency = result.Data.Currency
            });
        }

        // =============================================================
        //  POST /Customer/Bills/ProcessRazorpay
        //
        //  Pay.cshtml JS submits a hidden form here after the
        //  Razorpay modal closes with a successful payment.
        //
        //  Razorpay JS sends 3 values:
        //    razorpay_order_id   → razorpayOrderId
        //    razorpay_payment_id → razorpayPaymentId
        //    razorpay_signature  → razorpaySignature
        //
        //  We verify the HMAC signature, then run the 5-step
        //  completion transaction (same as cash).
        // =============================================================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessRazorpay(
            int billId,
            string razorpayOrderId,
            string razorpayPaymentId,
            string razorpaySignature)
        {
            var userId = _userManager.GetUserId(User)!;

            _logger.LogInformation(
                "Razorpay callback: bill #{BillId}, " +
                "order {OrderId}, payment {PaymentId}",
                billId, razorpayOrderId, razorpayPaymentId);

            var result = await _paymentService.ProcessRazorpayPaymentAsync(
                billId,
                userId,
                razorpayOrderId,
                razorpayPaymentId,
                razorpaySignature);

            if (!result.IsSuccess)
            {
                _logger.LogError(
                    "Razorpay processing failed for bill #{BillId}: {Error}",
                    billId, result.ErrorMessage);

                TempData["Error"] = result.ErrorMessage;
                return RedirectToAction("Index", "Requests");
            }

            TempData["Success"] =
                "Payment successful! Your request is now complete.";

            return RedirectToAction(
                nameof(Confirmation),
                new { paymentId = result.Data!.PaymentId });
        }

        // =============================================================
        //  GET /Customer/Bills/Confirmation?paymentId={id}
        //  Success receipt page shown after payment.
        // =============================================================
        [HttpGet]
        public async Task<IActionResult> Confirmation(int paymentId)
        {
            var payment = await _paymentService
                .GetPaymentByIdAsync(paymentId);

            if (payment == null)
            {
                TempData["Error"] = "Payment record not found.";
                return RedirectToAction("Index", "Requests");
            }

            return View(payment);
        }
    }
}