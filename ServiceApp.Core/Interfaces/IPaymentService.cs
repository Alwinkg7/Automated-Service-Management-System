using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;

namespace ServiceApp.Core.Interfaces
{
    /// <summary>Payment workflows: cash, Razorpay checkout and Razorpay webhooks.</summary>
    public interface IPaymentService
    {
        Task<Result<Bill>> GetBillForPaymentAsync(
            int requestId,
            string customerUserId);

        Task<Result<Payment>> ProcessCashPaymentAsync(
            int billId,
            string customerUserId);

        Task<Payment?> GetPaymentByIdAsync(int paymentId);

        /// <summary>
        /// The assigned technician confirms cash was collected for a Billed request:
        /// records a Cash payment and completes the request. Idempotent per bill.
        /// </summary>
        Task<Result<Payment>> ConfirmCashCollectedAsync(int requestId, string technicianUserId);

        /// <summary>Creates a Razorpay order for the bill (amount taken from the server-side bill).</summary>
        Task<Result<RazorpayOrderResult>> CreateRazorpayOrderAsync(
            int billId,
            string customerUserId);

        /// <summary>
        /// Verifies the checkout signature and completes the bill/request in one
        /// transaction. Idempotent on <paramref name="razorpayPaymentId"/>.
        /// </summary>
        Task<Result<Payment>> ProcessRazorpayPaymentAsync(
            int billId,
            string customerUserId,
            string razorpayOrderId,
            string razorpayPaymentId,
            string razorpaySignature);

        /// <summary>
        /// Completes a bill from a signature-verified Razorpay webhook event.
        /// Idempotent: a payment id that was already recorded returns success.
        /// </summary>
        Task<Result<Payment>> ProcessRazorpayWebhookAsync(RazorpayWebhookEvent webhookEvent);
    }
}
