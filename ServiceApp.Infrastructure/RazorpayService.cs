// =================================================================
//  RazorpayService.cs — ServiceApp.Infrastructure
//
//  Concrete implementation using the Razorpay .NET SDK.
//  All SDK calls are here — nowhere else in the app.
//
//  SIGNATURE VERIFICATION EXPLAINED:
//  Razorpay signs the payment using HMAC SHA256.
//  Input string: "{orderId}|{paymentId}"
//  Key: your KeySecret
//  We compute the same hash and compare with the one Razorpay sent.
//  If they match → payment is genuine (not tampered with).
//  If they don't → reject immediately (possible fraud/replay attack).
// =================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Razorpay.Api;
using ServiceApp.Core.Common;
using ServiceApp.Core.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;

namespace ServiceApp.Infrastructure
{
    public class RazorpayService : IRazorpayService
    {
        private readonly RazorpaySettings _settings;
        private readonly ILogger<RazorpayService> _logger;

        public RazorpayService(
            IOptions<RazorpaySettings> settings,
            ILogger<RazorpayService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        // =============================================================
        //  CREATE ORDER
        //  Call this when customer clicks "Pay Online".
        //  Razorpay creates an order on their end and returns an ID.
        //  This ID is passed to the frontend to open the checkout.
        // =============================================================
        public async Task<RazorpayOrderResult> CreateOrderAsync(
            decimal amount,
            string currency,
            string receiptId)
        {
            // Initialize Razorpay client with credentials
            var client = new RazorpayClient(
                _settings.KeyId,
                _settings.KeySecret);

            // Razorpay amounts are always in the smallest unit
            // INR → paise (1 rupee = 100 paise)
            // e.g. ₹850.50 → 85050
            var amountInPaise = (long)(amount * 100);

            var options = new Dictionary<string, object>
            {
                { "amount",   amountInPaise },
                { "currency", currency },
                { "receipt",  receiptId },

                // Notes are optional metadata — visible in dashboard
                { "notes", new Dictionary<string, string>
                    {
                        { "app", "ServiceApp" },
                        { "receipt", receiptId }
                    }
                }
            };

            // This is a blocking SDK call — wrap in Task.Run
            // so we don't block the thread pool
            var order = await Task.Run(() =>
                client.Order.Create(options));

            _logger.LogInformation(
                "Razorpay order created: {OrderId} for ₹{Amount}",
                (object)order["id"].ToString()!, (object)amount);

            return new RazorpayOrderResult
            {
                OrderId = order["id"].ToString()!,
                Amount = amountInPaise,
                Currency = currency,
                Receipt = receiptId
            };
        }

        // =============================================================
        //  VERIFY SIGNATURE
        //  Called after Razorpay JS sends payment details back.
        //  Returns true only if the signature is valid.
        //
        //  Algorithm:
        //  1. Concatenate orderId + "|" + paymentId
        //  2. Compute HMAC SHA256 with KeySecret
        //  3. Compare hex digest with received signature
        // =============================================================
        public bool VerifyPaymentSignature(
            string orderId,
            string paymentId,
            string signature)
        {
            try
            {
                // Build the message to sign
                var message = $"{orderId}|{paymentId}";

                // Compute HMAC SHA256
                var keyBytes = Encoding.UTF8.GetBytes(_settings.KeySecret);
                var msgBytes = Encoding.UTF8.GetBytes(message);

                using var hmac = new HMACSHA256(keyBytes);
                var hashBytes = hmac.ComputeHash(msgBytes);
                var computed = BitConverter
                    .ToString(hashBytes)
                    .Replace("-", "")
                    .ToLowerInvariant();

                // Compare with received signature
                var isValid = string.Equals(
                    computed, signature,
                    StringComparison.OrdinalIgnoreCase);

                if (!isValid)
                    _logger.LogWarning(
                        "Razorpay signature mismatch. " +
                        "OrderId: {OrderId}, PaymentId: {PaymentId}",
                        orderId, paymentId);

                return isValid;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Signature verification failed for order {OrderId}",
                    orderId);
                return false;
            }
        }

        // =============================================================
        //  REFUND PAYMENT
        //  Issues a refund against an existing Razorpay payment ID.
        //  Amount is in rupees — we convert to paise internally.
        //  Returns the Razorpay refund ID on success.
        //  Throws RazorpayException on failure — caller must handle.
        // =============================================================
        public async Task<string> RefundPaymentAsync(
            string paymentId,
            decimal amount,
            string reason)
        {
            // ================================================================

            var amountPaise = (long)(amount * 100);
            var url = $"https://api.razorpay.com/v1/payments/{paymentId}/refund";

            // Basic auth — base64(KeyId:KeySecret)
            var credentials = Convert.ToBase64String(
                System.Text.Encoding.UTF8.GetBytes(
                    $"{_settings.KeyId}:{_settings.KeySecret}"));

            // Build JSON body
            var body = System.Text.Json.JsonSerializer.Serialize(new
            {
                amount = amountPaise,
                notes = new { reason = reason }
            });

            using var http = new HttpClient();
            using var request = new HttpRequestMessage(System.Net.Http.HttpMethod.Post, url);

            request.Headers.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue(
                    "Basic", credentials);

            request.Content = new StringContent(
                body,
                System.Text.Encoding.UTF8,
                "application/json");

            _logger.LogInformation(
                "Calling Razorpay refund API for payment {PaymentId}, " +
                "amount ₹{Amount} ({Paise} paise)",
                paymentId, amount, amountPaise);

            HttpResponseMessage response;
            string responseBody;

            try
            {
                response = await http.SendAsync(request);
                responseBody = await response.Content.ReadAsStringAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "HTTP call to Razorpay refund API failed for {PaymentId}",
                    paymentId);
                throw new InvalidOperationException(
                    "Could not connect to Razorpay. " +
                    "Check your internet connection and try again.", ex);
            }

            _logger.LogInformation(
                "Razorpay refund API response: {StatusCode} — {Body}",
                (int)response.StatusCode, responseBody);

            // Parse the response
            var json = System.Text.Json.JsonDocument.Parse(responseBody).RootElement;

            if (!response.IsSuccessStatusCode)
            {
                // Extract Razorpay error description
                var errorDesc = "Unknown error";
                if (json.TryGetProperty("error", out var errorEl))
                {
                    if (errorEl.TryGetProperty("description", out var desc))
                        errorDesc = desc.GetString() ?? errorDesc;
                    else if (errorEl.TryGetProperty("code", out var code))
                        errorDesc = code.GetString() ?? errorDesc;
                }

                _logger.LogError(
                    "Razorpay refund failed for {PaymentId}. " +
                    "Status: {Status}. Error: {Error}",
                    paymentId, (int)response.StatusCode, errorDesc);

                throw new InvalidOperationException(
                    $"Razorpay refund failed: {errorDesc}");
            }

            // Extract refund ID from response
            if (!json.TryGetProperty("id", out var refundIdEl))
            {
                _logger.LogError(
                    "Razorpay refund response had no 'id' field. " +
                    "Body: {Body}", responseBody);
                throw new InvalidOperationException(
                    "Razorpay returned an unexpected response. " +
                    "Check your dashboard to confirm refund status.");
            }

            var refundId = refundIdEl.GetString()!;

            _logger.LogInformation(
                "Razorpay refund successful. RefundId: {RefundId}, " +
                "PaymentId: {PaymentId}, Amount: ₹{Amount}",
                refundId, paymentId, amount);

            return refundId;
        }
    }
}