using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Api.Contracts;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Api.Controllers;

/// <summary>
/// Customer loyalty, referral and bill discounts. Discounts are stored on the
/// bill and reflected in the Razorpay order amount.
/// </summary>
[Route("api/customer")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Customer")]
public class CustomerPromotionsApiController : ApiControllerBase
{
    private readonly IPromotionService _promotions;
    private readonly IBillDiscountService _discounts;

    public CustomerPromotionsApiController(IPromotionService promotions, IBillDiscountService discounts)
    {
        _promotions = promotions;
        _discounts = discounts;
    }

    /// <summary>Points balance, rupee value of redeemable points, referral code and ledger.</summary>
    [HttpGet("promotions")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Promotions()
    {
        var balance = await _promotions.GetPointsBalanceAsync(UserId);
        var history = await _promotions.GetPointsHistoryAsync(UserId);
        var referral = await _promotions.GetOrCreateReferralCodeAsync(UserId);

        return Ok(new
        {
            success = true,
            data = new
            {
                pointsBalance = balance,
                pointsValue = Math.Floor(balance / 100m) * 10m,
                referralCode = referral,
                history = history.Select(h => new
                {
                    points = h.Points,
                    transactionType = h.TransactionType,
                    note = h.Note,
                    requestId = h.RequestId,
                    transactedAt = h.TransactedAt,
                    expiresAt = h.ExpiresAt
                })
            }
        });
    }

    /// <summary>Checks a promo code against an amount (no bill, nothing recorded).</summary>
    [HttpPost("promotions/validate")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Validate([FromBody] ValidatePromoRequest dto)
    {
        var result = await _promotions.ValidatePromoCodeAsync(dto.Code, UserId, dto.BillAmount);
        if (!result.IsSuccess)
            return Fail(result);

        return Ok(new { success = true, data = new { discount = result.Data, message = $"Valid! You save ₹{result.Data:N2}." } });
    }

    /// <summary>Current subtotal, discount and payable amount of an own bill.</summary>
    [HttpGet("bills/{id:int}/quote")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Quote(int id) => QuoteResult(await _discounts.GetQuoteAsync(id, UserId));

    /// <summary>Validates a code against an unpaid bill without applying it.</summary>
    [HttpPost("bills/{id:int}/promo/preview")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> PreviewPromo(int id, [FromBody] BillPromoRequest dto) =>
        QuoteResult(await _discounts.PreviewPromoAsync(id, UserId, dto.Code));

    /// <summary>Applies a promo code to an unpaid bill (one code per bill).</summary>
    [HttpPost("bills/{id:int}/promo")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ApplyPromo(int id, [FromBody] BillPromoRequest dto) =>
        QuoteResult(await _discounts.ApplyPromoAsync(id, UserId, dto.Code));

    /// <summary>Redeems loyalty points against an unpaid bill (multiples of 100; 100 points = ₹10).</summary>
    [HttpPost("bills/{id:int}/points")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> RedeemPoints(int id, [FromBody] RedeemPointsRequest dto) =>
        QuoteResult(await _discounts.RedeemPointsAsync(id, UserId, dto.Points));

    private IActionResult QuoteResult(Core.Common.Result<Core.Models.Billing.BillQuote> result) =>
        result.IsSuccess
            ? Ok(new { success = true, data = ResponseMappers.Quote(result.Data!) })
            : Fail(result);
}
