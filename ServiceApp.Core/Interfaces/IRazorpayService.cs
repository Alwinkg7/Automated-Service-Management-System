// =================================================================
//  IRazorpayService.cs — ServiceApp.Core/Interfaces
//
//  Abstracts all direct Razorpay SDK calls.
//  The service layer (PaymentService) depends on this interface
//  — not on the Razorpay SDK directly.
//
//  WHY ABSTRACT IT?
//  - Unit tests can mock IRazorpayService without hitting Razorpay
//  - Swapping to Stripe or PayU = change only the implementation
//  - Keeps Razorpay SDK isolated to ServiceApp.Infrastructure
// =================================================================

using ServiceApp.Core.Common;

namespace ServiceApp.Core.Interfaces
{
    public interface IRazorpayService
    {
        // Create a Razorpay order on their server.
        // Returns the order object with Id, Amount, Currency.
        // Amount must be in smallest currency unit (paise for INR).
        // e.g. ₹850.00 → 85000 paise
        Task<RazorpayOrderResult> CreateOrderAsync(
            decimal amount,
            string currency,
            string receiptId);

        // Verify the payment signature sent by Razorpay JS after payment.
        // CRITICAL: never trust a payment without verifying the signature.
        // Razorpay signs: orderId + "|" + paymentId with your KeySecret.
        // We compute the same HMAC SHA256 and compare.
        bool VerifyPaymentSignature(
            string orderId,
            string paymentId,
            string signature);
    }

    // Result from CreateOrderAsync
    public class RazorpayOrderResult
    {
        public string OrderId { get; set; } = string.Empty;
        public long Amount { get; set; }   // in paise
        public string Currency { get; set; } = string.Empty;
        public string Receipt { get; set; } = string.Empty;
    }
}