using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ServiceApp.Core.Common;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Api.Controllers;

/// <summary>
/// Razorpay webhook receiver. Authenticated by the <c>X-Razorpay-Signature</c>
/// HMAC over the raw body (Razorpay:WebhookSecret). Handled and ignored events
/// return 200 so Razorpay stops retrying; only transient failures return 5xx.
/// </summary>
[Route("api/payments")]
[AllowAnonymous]
[DisableRateLimiting]
public class PaymentsWebhookController : ApiControllerBase
{
    private static readonly HashSet<string> CompletionEvents = new(StringComparer.Ordinal)
    {
        "order.paid",
        "payment.captured"
    };

    private readonly IRazorpayService _razorpay;
    private readonly IPaymentService _payments;
    private readonly ILogger<PaymentsWebhookController> _logger;

    public PaymentsWebhookController(
        IRazorpayService razorpay,
        IPaymentService payments,
        ILogger<PaymentsWebhookController> logger)
    {
        _razorpay = razorpay;
        _payments = payments;
        _logger = logger;
    }

    [HttpPost("webhook")]
    [RequestSizeLimit(256 * 1024)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Razorpay(
        [FromHeader(Name = "X-Razorpay-Signature")] string? signature,
        CancellationToken ct)
    {
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var payload = await reader.ReadToEndAsync(ct);

        if (string.IsNullOrEmpty(signature) || !_razorpay.VerifyWebhookSignature(payload, signature))
            return Fail(StatusCodes.Status401Unauthorized, "Invalid webhook signature.");

        var evt = _razorpay.ParseWebhookEvent(payload);
        if (evt == null)
            return Fail(StatusCodes.Status400BadRequest, "Malformed webhook payload.");

        if (!CompletionEvents.Contains(evt.Event))
        {
            _logger.LogInformation("Razorpay webhook {Event} ignored.", evt.Event);
            return Ok(new { success = true, handled = false });
        }

        var result = await _payments.ProcessRazorpayWebhookAsync(evt);
        if (result.IsSuccess)
            return Ok(new { success = true, handled = true });

        if (result.ErrorType is ErrorType.Unexpected or ErrorType.Unavailable)
            return Fail(result); // 5xx: let Razorpay retry later

        // Permanent problems (unknown bill, amount mismatch, already paid): acknowledge.
        _logger.LogWarning(
            "Razorpay webhook {Event} for payment {PaymentId} not applied: {Reason}",
            evt.Event, evt.PaymentId, result.ErrorMessage);
        return Ok(new { success = true, handled = false, reason = result.ErrorMessage });
    }
}
