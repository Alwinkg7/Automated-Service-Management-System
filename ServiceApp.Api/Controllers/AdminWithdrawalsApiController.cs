using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Api.Contracts;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Api.Controllers;

/// <summary>
/// Admin withdrawal processing through the WalletService state machine:
/// Pending → Approved → Paid, or Pending/Approved → Rejected (409 otherwise).
/// </summary>
[Route("api/admin/withdrawals")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminWithdrawalsApiController : ApiControllerBase
{
    private readonly IWalletService _wallet;

    public AdminWithdrawalsApiController(IWalletService wallet) => _wallet = wallet;

    /// <summary>All withdrawals (newest first), optionally filtered by status; status=open means Pending + Approved.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var items = string.Equals(status, "open", StringComparison.OrdinalIgnoreCase)
            ? await _wallet.GetOpenWithdrawalsAsync()
            : await _wallet.GetWithdrawalsAsync(status);
        return Ok(new { success = true, data = items.Select(ResponseMappers.Withdrawal) });
    }

    [HttpPost("{id:int}/approve")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Approve(int id) => Done(await _wallet.ApproveAsync(id, UserId));

    [HttpPost("{id:int}/pay")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarkPaid(int id) => Done(await _wallet.MarkPaidAsync(id, UserId));

    [HttpPost("{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reject(
        int id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] WithdrawalRejectRequest? dto) =>
        Done(await _wallet.RejectAsync(id, UserId, dto?.AdminNote));

    private IActionResult Done(Result<TechnicianWithdrawal> result) =>
        result.IsSuccess
            ? Ok(new { success = true, data = ResponseMappers.Withdrawal(result.Data!) })
            : Fail(result);
}
