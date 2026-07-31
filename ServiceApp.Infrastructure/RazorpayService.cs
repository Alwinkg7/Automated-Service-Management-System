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
    }
}