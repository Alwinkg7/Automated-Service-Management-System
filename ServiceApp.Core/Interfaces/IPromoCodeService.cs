using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;

namespace ServiceApp.Core.Interfaces;

/// <summary>Admin management of promo codes. Customer-facing validation lives in <see cref="IPromotionService"/>.</summary>
public interface IPromoCodeService
{
    Task<IReadOnlyList<PromoCode>> GetAllAsync();
    Task<Result<PromoCode>> GetAsync(int promoCodeId);

    /// <summary>Creates a code; a duplicate code is a <see cref="ErrorType.Conflict"/>.</summary>
    Task<Result<PromoCode>> CreateAsync(PromoCodeInput input);

    Task<Result<PromoCode>> UpdateAsync(int promoCodeId, PromoCodeInput input);
    Task<Result<PromoCode>> ToggleAsync(int promoCodeId);

    /// <summary>Deletes an unused code; a code that has been used is a <see cref="ErrorType.Conflict"/> (deactivate it instead).</summary>
    Task<Result> DeleteAsync(int promoCodeId);
}

/// <summary>Promo-code fields. DiscountType is Flat, Percentage or FirstOnly.</summary>
public record PromoCodeInput(
    string Code,
    string Description,
    string DiscountType,
    decimal DiscountValue,
    decimal? MaxDiscount,
    decimal MinimumAmount,
    int? MaxUses,
    DateTime? ExpiresAt,
    bool IsActive);
