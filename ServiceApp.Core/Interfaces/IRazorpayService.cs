using ServiceApp.Core.Common;

namespace ServiceApp.Core.Interfaces
{
    /// <summary>
    /// Adapter over the Razorpay gateway. Keeps the SDK isolated in
    /// ServiceApp.Infrastructure so services can be unit-tested with a mock.
    /// </summary>
    public interface IRazorpayService
    {
        /// <summary>
        /// Creates a Razorpay order. <paramref name="amount"/> is in rupees and is
        /// converted to paise internally. <paramref name="billId"/> is stored in the
        /// order notes so webhooks can be correlated back to the bill.
        /// </summary>
        /// <exception cref="UpstreamServiceException">Razorpay rejected the request or was unreachable.</exception>
        Task<RazorpayOrderResult> CreateOrderAsync(
            decimal amount,
            string currency,
            string receiptId,
            int? billId = null);

        /// <summary>
        /// Verifies the checkout signature: HMAC-SHA256 of <c>orderId|paymentId</c>
        /// keyed with the KeySecret (constant-time comparison).
        /// </summary>
        bool VerifyPaymentSignature(
            string orderId,
            string paymentId,
            string signature);

        /// <summary>
        /// Verifies the <c>X-Razorpay-Signature</c> header: HMAC-SHA256 of the raw
        /// request body keyed with the WebhookSecret (constant-time comparison).
        /// </summary>
        bool VerifyWebhookSignature(string payload, string signature);

        /// <summary>
        /// Extracts the payment/order fields from a webhook body. Returns null when
        /// the payload is malformed.
        /// </summary>
        RazorpayWebhookEvent? ParseWebhookEvent(string payload);

        /// <summary>Refunds a captured payment and returns the Razorpay refund id.</summary>
        /// <exception cref="UpstreamServiceException">The refund call failed.</exception>
        Task<string> RefundPaymentAsync(
            string paymentId,
            decimal amount,
            string reason);
    }

    /// <summary>Result of <see cref="IRazorpayService.CreateOrderAsync"/>.</summary>
    public class RazorpayOrderResult
    {
        public string OrderId { get; set; } = string.Empty;

        /// <summary>Amount in paise.</summary>
        public long Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Receipt { get; set; } = string.Empty;
    }
}
