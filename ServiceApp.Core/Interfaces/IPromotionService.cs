// =================================================================
//  IPromotionService.cs — ServiceApp.Core/Interfaces
//
//  All promotion + loyalty business logic lives here.
//  Called by:
//  - BillService (apply promo, award points)
//  - AccountController (generate referral code on signup)
//  - Customer controllers (check promo, view points)
// =================================================================

using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;

namespace ServiceApp.Core.Interfaces
{
    public interface IPromotionService
    {
        // ── Promo codes ────────────────────────────────────────────

        // Validate a promo code for a customer + bill amount.
        // Returns the discount amount or an error.
        Task<Result<decimal>> ValidatePromoCodeAsync(
            string code,
            string customerId,
            decimal billAmount);

        // Apply a promo code — inserts PromoUsage row,
        // increments PromoCode.UsedCount.
        Task<Result<decimal>> ApplyPromoCodeAsync(
            string code,
            string customerId,
            int requestId,
            decimal billAmount);

        // ── Loyalty points ─────────────────────────────────────────

        // Get current points balance for a customer.
        Task<int> GetPointsBalanceAsync(string customerId);

        // Award points after a completed payment.
        // 1 point per ₹10 spent. Expires in 12 months.
        Task AwardPointsAsync(
            string customerId,
            int requestId,
            decimal amountPaid);

        // Redeem points as a bill discount.
        // 100 points = ₹10 discount.
        Task<Result<decimal>> RedeemPointsAsync(
            string customerId,
            int requestId,
            int pointsToRedeem);

        // Get full points transaction history.
        Task<IEnumerable<LoyaltyLedger>> GetPointsHistoryAsync(
            string customerId);

        // ── Referral system ────────────────────────────────────────

        // Get or create a referral code for a customer.
        Task<string> GetOrCreateReferralCodeAsync(string customerId);

        // Apply a referral code during signup.
        // Awards points to both the new user and the referrer.
        Task ApplyReferralCodeAsync(
            string newCustomerId,
            string referralCode);
    }
}