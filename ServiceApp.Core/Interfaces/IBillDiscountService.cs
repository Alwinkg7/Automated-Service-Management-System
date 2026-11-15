using ServiceApp.Core.Common;
using ServiceApp.Core.Models.Billing;

namespace ServiceApp.Core.Interfaces;

/// <summary>
/// Discounts on an unpaid bill (promo code, loyalty points) before payment.
/// The resulting payable amount is what the Razorpay order and every payment
/// check use. Applying a discount invalidates any Razorpay order already created.
/// </summary>
public interface IBillDiscountService
{
    /// <summary>Current subtotal, discount and payable amount for the bill owner.</summary>
    Task<Result<BillQuote>> GetQuoteAsync(int billId, string customerUserId);

    /// <summary>Validates a code against the bill without applying it.</summary>
    Task<Result<BillQuote>> PreviewPromoAsync(int billId, string customerUserId, string code);

    /// <summary>Applies a promo code to the bill (one code per bill) and records its usage.</summary>
    Task<Result<BillQuote>> ApplyPromoAsync(int billId, string customerUserId, string code);

    /// <summary>Redeems loyalty points (multiples of 100; 100 points = 10 rupees) against the bill.</summary>
    Task<Result<BillQuote>> RedeemPointsAsync(int billId, string customerUserId, int points);

    /// <summary>
    /// Applies the optional checkout discounts idempotently: a promo code already
    /// on the bill is not re-applied, and <paramref name="redeemMaxPoints"/> redeems
    /// the largest valid number of points (none when the balance is too low).
    /// </summary>
    Task<Result<BillQuote>> PrepareCheckoutAsync(int billId, string customerUserId, string? promoCode, bool redeemMaxPoints);
}
