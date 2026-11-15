using System.ComponentModel.DataAnnotations;

namespace ServiceApp.Api.Contracts;

/// <summary>Optional body of POST /api/customer/bills/{id}/razorpay/order.</summary>
public record RazorpayOrderRequest(
    [StringLength(30)] string? PromoCode,
    bool? RedeemPoints);

/// <summary>POST /api/customer/promotions/validate (code check against an amount, no bill).</summary>
public record ValidatePromoRequest(
    [Required, StringLength(30)] string Code,
    [Range(0, 10_000_000)] decimal BillAmount);

/// <summary>POST /api/customer/bills/{id}/promo and /promo/preview.</summary>
public record BillPromoRequest([Required, StringLength(30)] string Code);

/// <summary>POST /api/customer/bills/{id}/points.</summary>
public record RedeemPointsRequest([Range(100, 1_000_000)] int Points);
