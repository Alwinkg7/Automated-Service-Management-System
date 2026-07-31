// =================================================================
//  IPaymentService.cs — updated with Razorpay methods
// =================================================================

using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;

namespace ServiceApp.Core.Interfaces
{
    public interface IPaymentService
    {
        // ── Existing methods (keep these) ──────────────────────────

        Task<Result<Bill>> GetBillForPaymentAsync(
            int requestId,
            string customerUserId);

        Task<Result<Payment>> ProcessCashPaymentAsync(
            int billId,
            string customerUserId);

        Task<Payment?> GetPaymentByIdAsync(int paymentId);

        // ── New Razorpay methods ───────────────────────────────────

        // Step 1: Create a Razorpay order.
        // Called when customer clicks "Pay Online".
        // Returns order details needed by the frontend JS checkout.
        Task<Result<RazorpayOrderResult>> CreateRazorpayOrderAsync(
            int billId,
            string customerUserId);

        // Step 2: Verify and process after Razorpay checkout completes.
        // Called with the payment details sent by Razorpay JS.
        // Verifies signature then runs the full completion transaction.
        Task<Result<Payment>> ProcessRazorpayPaymentAsync(
            int billId,
            string customerUserId,
            string razorpayOrderId,
            string razorpayPaymentId,
            string razorpaySignature);
    }
}