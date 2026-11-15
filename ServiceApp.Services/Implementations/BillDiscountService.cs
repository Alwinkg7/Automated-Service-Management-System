using Microsoft.Extensions.Logging;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Core.Models.Billing;

namespace ServiceApp.Services.Implementations;

/// <summary>
/// Applies promo codes and loyalty points to an unpaid bill. The discount is
/// stored on the bill (so the Razorpay order, checkout and webhook all charge
/// the same payable amount) and any existing Razorpay order is invalidated.
/// The payable amount never drops below <see cref="MinimumPayable"/>.
/// </summary>
public class BillDiscountService : IBillDiscountService
{
    /// <summary>Razorpay minimum charge is 1 rupee.</summary>
    public const decimal MinimumPayable = 1m;

    private readonly IUnitOfWork _uow;
    private readonly IPromotionService _promotions;
    private readonly ILogger<BillDiscountService> _logger;

    public BillDiscountService(IUnitOfWork uow, IPromotionService promotions, ILogger<BillDiscountService> logger)
    {
        _uow = uow;
        _promotions = promotions;
        _logger = logger;
    }

    public async Task<Result<BillQuote>> GetQuoteAsync(int billId, string customerUserId)
    {
        var (bill, error) = await LoadPayableBillAsync(billId, customerUserId, requireUnpaid: false);
        return bill == null ? Result<BillQuote>.Failure(error!.Value.Message, error.Value.Type) : Result<BillQuote>.Success(Quote(bill));
    }

    public async Task<Result<BillQuote>> PreviewPromoAsync(int billId, string customerUserId, string code)
    {
        var (bill, error) = await LoadPayableBillAsync(billId, customerUserId, requireUnpaid: true);
        if (bill == null) return Result<BillQuote>.Failure(error!.Value.Message, error.Value.Type);

        if (bill.PromoCode != null)
            return Result<BillQuote>.Failure($"Promo code {bill.PromoCode} is already applied to this bill.", ErrorType.Conflict);

        var validation = await _promotions.ValidatePromoCodeAsync(code, customerUserId, bill.PayableAmount);
        if (!validation.IsSuccess)
            return Result<BillQuote>.Failure(validation.ErrorMessage!, validation.ErrorType);

        var discount = Clamp(bill, validation.Data);
        return Result<BillQuote>.Success(new BillQuote(
            bill.Id, bill.TotalAmount, bill.DiscountAmount + discount,
            bill.TotalAmount - bill.DiscountAmount - discount, code.Trim().ToUpperInvariant(), bill.PointsRedeemed));
    }

    public async Task<Result<BillQuote>> ApplyPromoAsync(int billId, string customerUserId, string code)
    {
        var preview = await PreviewPromoAsync(billId, customerUserId, code);
        if (!preview.IsSuccess) return preview;

        var bill = (await _uow.Bills.GetByIdAsync(billId))!;
        var promo = (await _uow.PromoCodes.GetByCodeAsync(code.Trim().ToUpperInvariant()))!;
        var discount = preview.Data!.Discount - bill.DiscountAmount;

        await _uow.BeginTransactionAsync();
        try
        {
            await _uow.PromoCodes.AddUsageAsync(new PromoUsage
            {
                PromoCodeId = promo.PromoCodeId,
                CustomerId = customerUserId,
                RequestId = bill.ServiceRequestId,
                DiscountApplied = discount,
                UsedAt = DateTime.UtcNow
            });
            promo.UsedCount++;
            _uow.PromoCodes.Update(promo);

            bill.PromoCode = promo.Code;
            bill.DiscountAmount += discount;
            bill.RazorpayOrderId = null; // the old order was for the old amount
            _uow.Bills.Update(bill);

            await _uow.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync();
            _uow.ClearChanges();
            _logger.LogError(ex, "Applying promo {Code} to bill #{BillId} failed", promo.Code, billId);
            return Result<BillQuote>.Failure("Could not apply the promo code. Please try again.", ErrorType.Unexpected);
        }

        _logger.LogInformation("Promo {Code} applied to bill #{BillId}: -{Discount}", promo.Code, billId, discount);
        return Result<BillQuote>.Success(Quote(bill));
    }

    public async Task<Result<BillQuote>> RedeemPointsAsync(int billId, string customerUserId, int points)
    {
        var (bill, error) = await LoadPayableBillAsync(billId, customerUserId, requireUnpaid: true);
        if (bill == null) return Result<BillQuote>.Failure(error!.Value.Message, error.Value.Type);

        // Value check before spending: 100 points = 10 rupees.
        var value = points / 100 * 10m;
        if (points > 0 && value > bill.PayableAmount - MinimumPayable)
            return Result<BillQuote>.Failure(
                $"You can redeem at most ₹{bill.PayableAmount - MinimumPayable:N2} on this bill.", ErrorType.BusinessRule);

        await _uow.BeginTransactionAsync();
        try
        {
            var redeemed = await _promotions.RedeemPointsAsync(customerUserId, bill.ServiceRequestId, points);
            if (!redeemed.IsSuccess)
            {
                await _uow.RollbackTransactionAsync();
                _uow.ClearChanges();
                return Result<BillQuote>.Failure(redeemed.ErrorMessage!, redeemed.ErrorType);
            }

            bill.DiscountAmount += redeemed.Data;
            bill.PointsRedeemed += points;
            bill.RazorpayOrderId = null;
            _uow.Bills.Update(bill);

            await _uow.CommitTransactionAsync();
        }
        catch (Exception ex)
        {
            await _uow.RollbackTransactionAsync();
            _uow.ClearChanges();
            _logger.LogError(ex, "Redeeming points on bill #{BillId} failed", billId);
            return Result<BillQuote>.Failure("Could not redeem points. Please try again.", ErrorType.Unexpected);
        }

        return Result<BillQuote>.Success(Quote(bill));
    }

    public async Task<Result<BillQuote>> PrepareCheckoutAsync(
        int billId, string customerUserId, string? promoCode, bool redeemMaxPoints)
    {
        var quote = await GetQuoteAsync(billId, customerUserId);
        if (!quote.IsSuccess) return quote;

        if (!string.IsNullOrWhiteSpace(promoCode)
            && !string.Equals(quote.Data!.PromoCode, promoCode.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            quote = await ApplyPromoAsync(billId, customerUserId, promoCode);
            if (!quote.IsSuccess) return quote;
        }

        if (redeemMaxPoints && quote.Data!.PointsRedeemed == 0) // only once per bill, so retries do not redeem again
        {
            var balance = await _promotions.GetPointsBalanceAsync(customerUserId);
            var maxByAmount = (int)Math.Floor((quote.Data!.Payable - MinimumPayable) / 10m) * 100;
            var points = Math.Min(balance / 100 * 100, maxByAmount);
            if (points >= 100)
            {
                quote = await RedeemPointsAsync(billId, customerUserId, points);
                if (!quote.IsSuccess) return quote;
            }
        }

        return quote;
    }

    // ── Helpers ───────────────────────────────────────────────────────

    private static BillQuote Quote(Bill bill) =>
        new(bill.Id, bill.TotalAmount, bill.DiscountAmount, bill.PayableAmount, bill.PromoCode, bill.PointsRedeemed);

    private static decimal Clamp(Bill bill, decimal discount) =>
        Math.Max(0m, Math.Min(discount, bill.PayableAmount - MinimumPayable));

    private async Task<(Bill? Bill, (string Message, ErrorType Type)? Error)> LoadPayableBillAsync(
        int billId, string customerUserId, bool requireUnpaid)
    {
        var bill = await _uow.Bills.GetByIdAsync(billId);
        if (bill == null) return (null, ("Bill not found.", ErrorType.NotFound));

        var request = await _uow.ServiceRequests.GetByIdAsync(bill.ServiceRequestId);
        if (request == null) return (null, ("Request not found.", ErrorType.NotFound));
        if (request.CustomerId != customerUserId) return (null, ("This bill does not belong to you.", ErrorType.Forbidden));

        if (requireUnpaid && (bill.PaymentStatus == PaymentStatus.Paid || request.Status != RequestStatus.Billed))
            return (null, ("Discounts can only be applied to an unpaid bill.", ErrorType.Conflict));

        return (bill, null);
    }
}
