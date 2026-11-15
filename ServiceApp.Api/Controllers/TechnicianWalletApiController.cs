using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Api.Contracts;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Api.Controllers;

/// <summary>Technician wallet: earnings, payouts and withdrawals (balance computed server-side).</summary>
[Route("api/technician/wallet")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Technician")]
public class TechnicianWalletApiController : ApiControllerBase
{
    private readonly IWalletService _wallet;

    public TechnicianWalletApiController(IWalletService wallet) => _wallet = wallet;

    /// <summary>Earnings summary, last 6 months, payouts and withdrawals in one call.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Summary()
    {
        var s = await _wallet.GetSummaryAsync(UserId);
        var payouts = await _wallet.GetPayoutHistoryAsync(UserId);
        var withdrawals = await _wallet.GetWithdrawalsForTechnicianAsync(UserId);

        return Ok(new
        {
            success = true,
            data = new
            {
                grossEarningsAllTime = s.GrossEarningsAllTime,
                commissionAllTime = s.CommissionAllTime,
                netEarningsAllTime = s.NetEarningsAllTime,
                grossEarningsThisMonth = s.GrossEarningsThisMonth,
                netEarningsThisMonth = s.NetEarningsThisMonth,
                grossEarningsThisWeek = s.GrossEarningsThisWeek,
                netEarningsThisWeek = s.NetEarningsThisWeek,
                availableToWithdraw = s.AvailableToWithdraw,
                pendingWithdrawal = s.PendingWithdrawal,
                totalWithdrawn = s.TotalWithdrawn,
                totalJobsCompleted = s.TotalJobsCompleted,
                averagePerJob = s.AveragePerJob,
                commissionRate = s.CommissionRate,
                monthly = s.GrossLast6Months.Select(m => new
                {
                    label = m.Label,
                    gross = m.Amount,
                    net = Math.Round(m.Amount * (1 - s.CommissionRate), 2)
                }),
                daily = s.GrossLast30Days.Select(d => new { label = d.Label, gross = d.Amount }),
                payouts = payouts.Select(Payout),
                withdrawals = withdrawals.Select(ResponseMappers.Withdrawal)
            }
        });
    }

    /// <summary>Paid jobs with gross, commission and net, newest first.</summary>
    [HttpGet("earnings")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Earnings() =>
        Ok(new { success = true, data = (await _wallet.GetPayoutHistoryAsync(UserId)).Select(Payout) });

    [HttpGet("withdrawals")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Withdrawals() =>
        Ok(new { success = true, data = (await _wallet.GetWithdrawalsForTechnicianAsync(UserId)).Select(ResponseMappers.Withdrawal) });

    /// <summary>Requests a withdrawal; the amount must not exceed the server-computed balance (422).</summary>
    [HttpPost("withdrawals")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> RequestWithdrawal([FromBody] WithdrawalRequestDto dto)
    {
        var result = await _wallet.RequestWithdrawalAsync(UserId, dto.Amount, dto.UpiId);
        if (!result.IsSuccess)
            return Fail(result);

        return StatusCode(StatusCodes.Status201Created,
            new { success = true, data = new { id = result.Data!.WithdrawalId, withdrawal = ResponseMappers.Withdrawal(result.Data) } });
    }

    private static object Payout(Core.Models.Reports.PayoutEntry p) => new
    {
        requestId = p.RequestId,
        billId = p.BillId,
        customerName = p.CustomerName,
        category = p.Category,
        grossAmount = p.GrossAmount,
        commission = p.Commission,
        netAmount = p.NetAmount,
        paymentMethod = p.PaymentMethod,
        paidAt = p.PaidAt
    };
}
