using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Razorpay.Api;
using ServiceApp.Core.Common;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Infrastructure
{
    /// <summary>
    /// Razorpay gateway adapter. Order creation uses the Razorpay SDK; refunds use
    /// the REST API through an <see cref="HttpClient"/> supplied by IHttpClientFactory.
    /// </summary>
    public class RazorpayService : IRazorpayService
    {
        private const string Provider = "Razorpay";
        private const string BillIdNote = "billId";

        private readonly HttpClient _http;
        private readonly RazorpaySettings _settings;
        private readonly ILogger<RazorpayService> _logger;

        public RazorpayService(
            HttpClient http,
            IOptions<RazorpaySettings> settings,
            ILogger<RazorpayService> logger)
        {
            _http = http;
            _settings = settings.Value;
            _logger = logger;
        }

        // ── Orders ────────────────────────────────────────────────────

        public async Task<RazorpayOrderResult> CreateOrderAsync(
            decimal amount,
            string currency,
            string receiptId,
            int? billId = null)
        {
            var amountInPaise = ToPaise(amount);

            var notes = new Dictionary<string, string>
            {
                { "app", "ServiceApp" },
                { "receipt", receiptId }
            };
            if (billId.HasValue)
                notes[BillIdNote] = billId.Value.ToString(CultureInfo.InvariantCulture);

            var options = new Dictionary<string, object>
            {
                { "amount", amountInPaise },
                { "currency", currency },
                { "receipt", receiptId },
                { "notes", notes }
            };

            try
            {
                var client = new RazorpayClient(_settings.KeyId, _settings.KeySecret);

                // The SDK is synchronous; keep it off the request thread.
                var order = await Task.Run(() => client.Order.Create(options));
                string orderId = order["id"].ToString();

                _logger.LogInformation(
                    "Razorpay order created: {OrderId} for {Amount}", orderId, amount);

                return new RazorpayOrderResult
                {
                    OrderId = orderId,
                    Amount = amountInPaise,
                    Currency = currency,
                    Receipt = receiptId
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Razorpay order creation failed for receipt {Receipt}", receiptId);
                throw new UpstreamServiceException(Provider, "Could not create the Razorpay order.", ex);
            }
        }

        // ── Signatures ────────────────────────────────────────────────

        public bool VerifyPaymentSignature(string orderId, string paymentId, string signature)
        {
            var isValid = VerifyHmac($"{orderId}|{paymentId}", _settings.KeySecret, signature);

            if (!isValid)
                _logger.LogWarning(
                    "Razorpay checkout signature mismatch. OrderId: {OrderId}, PaymentId: {PaymentId}",
                    orderId, paymentId);

            return isValid;
        }

        public bool VerifyWebhookSignature(string payload, string signature)
        {
            if (string.IsNullOrEmpty(_settings.WebhookSecret))
            {
                _logger.LogError("Razorpay webhook received but Razorpay:WebhookSecret is not configured.");
                return false;
            }

            var isValid = VerifyHmac(payload, _settings.WebhookSecret, signature);
            if (!isValid)
                _logger.LogWarning("Razorpay webhook signature mismatch.");

            return isValid;
        }

        public RazorpayWebhookEvent? ParseWebhookEvent(string payload)
        {
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                var eventName = root.TryGetProperty("event", out var ev) ? ev.GetString() ?? "" : "";
                if (!root.TryGetProperty("payload", out var body))
                    return new RazorpayWebhookEvent { Event = eventName };

                JsonElement payment = default, order = default;
                var hasPayment = body.TryGetProperty("payment", out var p) && p.TryGetProperty("entity", out payment);
                var hasOrder = body.TryGetProperty("order", out var o) && o.TryGetProperty("entity", out order);

                string? paymentId = hasPayment ? GetString(payment, "id") : null;
                string? orderId = hasPayment ? GetString(payment, "order_id") : null;
                orderId ??= hasOrder ? GetString(order, "id") : null;

                long amount = 0;
                if (hasPayment && payment.TryGetProperty("amount", out var a) && a.TryGetInt64(out var pa))
                    amount = pa;
                else if (hasOrder && order.TryGetProperty("amount_paid", out var ap) && ap.TryGetInt64(out var oa))
                    amount = oa;

                int? billId = null;
                if (hasOrder) billId = ReadBillId(order);
                if (billId == null && hasPayment) billId = ReadBillId(payment);

                return new RazorpayWebhookEvent
                {
                    Event = eventName,
                    PaymentId = paymentId,
                    OrderId = orderId,
                    AmountPaise = amount,
                    BillId = billId
                };
            }
            catch (JsonException ex)
            {
                _logger.LogWarning(ex, "Malformed Razorpay webhook payload.");
                return null;
            }
        }

        // ── Refunds ───────────────────────────────────────────────────

        public async Task<string> RefundPaymentAsync(string paymentId, decimal amount, string reason)
        {
            var amountPaise = ToPaise(amount);
            var url = $"https://api.razorpay.com/v1/payments/{Uri.EscapeDataString(paymentId)}/refund";
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{_settings.KeyId}:{_settings.KeySecret}"));

            using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, url)
            {
                Content = new StringContent(
                    JsonSerializer.Serialize(new { amount = amountPaise, notes = new { reason } }),
                    Encoding.UTF8,
                    "application/json")
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", credentials);

            _logger.LogInformation(
                "Calling Razorpay refund API for payment {PaymentId}, amount {Amount} ({Paise} paise)",
                paymentId, amount, amountPaise);

            HttpResponseMessage response;
            string responseBody;
            try
            {
                response = await _http.SendAsync(request);
                responseBody = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
            {
                _logger.LogError(ex, "HTTP call to Razorpay refund API failed for {PaymentId}", paymentId);
                throw new UpstreamServiceException(Provider, "Could not connect to Razorpay.", ex);
            }

            using (response)
            {
                JsonElement json;
                try
                {
                    json = JsonDocument.Parse(responseBody).RootElement;
                }
                catch (JsonException ex)
                {
                    throw new UpstreamServiceException(Provider, "Razorpay returned an unreadable response.", ex);
                }

                if (!response.IsSuccessStatusCode)
                {
                    var errorDesc = "Unknown error";
                    if (json.TryGetProperty("error", out var errorEl))
                        errorDesc = GetString(errorEl, "description") ?? GetString(errorEl, "code") ?? errorDesc;

                    _logger.LogError(
                        "Razorpay refund failed for {PaymentId}. Status: {Status}. Error: {Error}",
                        paymentId, (int)response.StatusCode, errorDesc);

                    throw new UpstreamServiceException(Provider, $"Razorpay refund failed: {errorDesc}");
                }

                var refundId = GetString(json, "id")
                    ?? throw new UpstreamServiceException(Provider,
                        "Razorpay returned an unexpected refund response. Check the dashboard for refund status.");

                _logger.LogInformation(
                    "Razorpay refund successful. RefundId: {RefundId}, PaymentId: {PaymentId}, Amount: {Amount}",
                    refundId, paymentId, amount);

                return refundId;
            }
        }

        // ── Helpers ───────────────────────────────────────────────────

        private static long ToPaise(decimal amount) =>
            (long)decimal.Round(amount * 100m, 0, MidpointRounding.AwayFromZero);

        private static bool VerifyHmac(string message, string secret, string signature)
        {
            if (string.IsNullOrEmpty(secret) || string.IsNullOrEmpty(signature))
                return false;

            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var computed = hmac.ComputeHash(Encoding.UTF8.GetBytes(message));

            byte[] provided;
            try
            {
                provided = Convert.FromHexString(signature);
            }
            catch (FormatException)
            {
                return false;
            }

            return CryptographicOperations.FixedTimeEquals(computed, provided);
        }

        private static string? GetString(JsonElement element, string property) =>
            element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty(property, out var value)
            && value.ValueKind == JsonValueKind.String
                ? value.GetString()
                : null;

        private static int? ReadBillId(JsonElement entity)
        {
            if (entity.TryGetProperty("notes", out var notes) && notes.ValueKind == JsonValueKind.Object
                && int.TryParse(GetString(notes, BillIdNote), out var fromNotes))
                return fromNotes;

            // Receipt format written by PaymentService: bill_{billId}_{yyyyMMdd}
            var receipt = GetString(entity, "receipt");
            if (receipt != null && receipt.StartsWith("bill_", StringComparison.Ordinal))
            {
                var parts = receipt.Split('_');
                if (parts.Length >= 2 && int.TryParse(parts[1], out var fromReceipt))
                    return fromReceipt;
            }

            return null;
        }
    }
}
