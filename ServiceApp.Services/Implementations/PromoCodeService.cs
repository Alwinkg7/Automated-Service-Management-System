using Microsoft.Extensions.Logging;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Services.Implementations;

/// <summary>Promo-code administration (create, edit, toggle, delete unused).</summary>
public class PromoCodeService : IPromoCodeService
{
    private static readonly string[] DiscountTypes = ["Flat", "Percentage", "FirstOnly"];

    private readonly IUnitOfWork _uow;
    private readonly ILogger<PromoCodeService> _logger;

    public PromoCodeService(IUnitOfWork uow, ILogger<PromoCodeService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PromoCode>> GetAllAsync() =>
        (await _uow.PromoCodes.GetAllAsync()).OrderByDescending(p => p.CreatedAt).ToList();

    public async Task<Result<PromoCode>> GetAsync(int promoCodeId) =>
        await _uow.PromoCodes.GetByIdAsync(promoCodeId) is { } code
            ? Result<PromoCode>.Success(code)
            : Result<PromoCode>.Failure("Promo code not found.", ErrorType.NotFound);

    public async Task<Result<PromoCode>> CreateAsync(PromoCodeInput input)
    {
        var error = Validate(input);
        if (error != null) return Result<PromoCode>.Failure(error);

        var code = Normalize(input.Code);
        if (await _uow.PromoCodes.GetByCodeAsync(code) != null)
            return Result<PromoCode>.Failure("This code already exists.", ErrorType.Conflict);

        var promo = new PromoCode { Code = code, UsedCount = 0, CreatedAt = DateTime.UtcNow };
        Apply(promo, input);
        await _uow.PromoCodes.AddAsync(promo);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Promo code {Code} created", code);
        return Result<PromoCode>.Success(promo);
    }

    public async Task<Result<PromoCode>> UpdateAsync(int promoCodeId, PromoCodeInput input)
    {
        var promo = await _uow.PromoCodes.GetByIdAsync(promoCodeId);
        if (promo == null) return Result<PromoCode>.Failure("Promo code not found.", ErrorType.NotFound);

        var error = Validate(input);
        if (error != null) return Result<PromoCode>.Failure(error);

        var code = Normalize(input.Code);
        if (code != promo.Code)
        {
            if (promo.UsedCount > 0)
                return Result<PromoCode>.Failure("A code that has been used cannot be renamed.", ErrorType.Conflict);
            if (await _uow.PromoCodes.GetByCodeAsync(code) != null)
                return Result<PromoCode>.Failure("This code already exists.", ErrorType.Conflict);
            promo.Code = code;
        }

        Apply(promo, input);
        _uow.PromoCodes.Update(promo);
        await _uow.SaveChangesAsync();
        return Result<PromoCode>.Success(promo);
    }

    public async Task<Result<PromoCode>> ToggleAsync(int promoCodeId)
    {
        var promo = await _uow.PromoCodes.GetByIdAsync(promoCodeId);
        if (promo == null) return Result<PromoCode>.Failure("Promo code not found.", ErrorType.NotFound);

        promo.IsActive = !promo.IsActive;
        _uow.PromoCodes.Update(promo);
        await _uow.SaveChangesAsync();
        return Result<PromoCode>.Success(promo);
    }

    public async Task<Result> DeleteAsync(int promoCodeId)
    {
        var promo = await _uow.PromoCodes.GetByIdAsync(promoCodeId);
        if (promo == null) return Result.Failure("Promo code not found.", ErrorType.NotFound);

        if (promo.UsedCount > 0)
            return Result.Failure("This code has been used; deactivate it instead.", ErrorType.Conflict);

        _uow.PromoCodes.Remove(promo);
        await _uow.SaveChangesAsync();
        return Result.Success();
    }

    private static string Normalize(string code) => code.Trim().ToUpperInvariant();

    private static void Apply(PromoCode promo, PromoCodeInput input)
    {
        promo.Description = input.Description?.Trim() ?? string.Empty;
        promo.DiscountType = input.DiscountType;
        promo.DiscountValue = input.DiscountValue;
        promo.MaxDiscount = input.MaxDiscount;
        promo.MinimumAmount = input.MinimumAmount;
        promo.MaxUses = input.MaxUses;
        promo.ExpiresAt = input.ExpiresAt;
        promo.IsActive = input.IsActive;
    }

    private static string? Validate(PromoCodeInput input)
    {
        var code = input.Code?.Trim() ?? string.Empty;
        if (code.Length is < 3 or > 30 || !code.All(char.IsLetterOrDigit))
            return "Code must be 3-30 letters or digits.";
        if (!DiscountTypes.Contains(input.DiscountType))
            return "Discount type must be Flat, Percentage or FirstOnly.";
        if (input.DiscountValue <= 0)
            return "Discount value must be greater than zero.";
        if (input.DiscountType == "Percentage" && input.DiscountValue > 100)
            return "A percentage discount cannot exceed 100.";
        if (input.MinimumAmount < 0 || input.MaxDiscount is < 0)
            return "Amounts cannot be negative.";
        if (input.MaxUses is <= 0)
            return "Max uses must be positive when set.";
        if (input.ExpiresAt is { } expires && expires <= DateTime.UtcNow)
            return "Expiry must be in the future.";
        return null;
    }
}
