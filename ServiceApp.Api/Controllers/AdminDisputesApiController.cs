using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Api.Contracts;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Api.Controllers;

/// <summary>Admin dispute handling: list, detail, review, resolve, Razorpay refund.</summary>
[Route("api/admin/disputes")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Admin")]
public class AdminDisputesApiController : ApiControllerBase
{
    private readonly IDisputeService _disputes;

    public AdminDisputesApiController(IDisputeService disputes) => _disputes = disputes;

    /// <summary>Disputes, optionally filtered by status (Open, UnderReview, Resolved), with counts.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List([FromQuery] string? status)
    {
        var disputes = await _disputes.GetAllDisputesAsync(string.IsNullOrWhiteSpace(status) ? null : status);
        var counts = await _disputes.GetCountsAsync();
        return Ok(new { success = true, data = disputes.Select(ResponseMappers.Dispute), counts });
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(int id) =>
        await _disputes.GetDisputeDetailsAsync(id) is { } d
            ? Ok(new { success = true, data = ResponseMappers.Dispute(d) })
            : Fail(StatusCodes.Status404NotFound, "Dispute not found.");

    /// <summary>Open → UnderReview. 409 if the dispute is not Open.</summary>
    [HttpPost("{id:int}/review")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Review(
        int id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] DisputeReviewRequest? dto)
    {
        var result = await _disputes.StartReviewAsync(id, dto?.AdminNote ?? string.Empty);
        return result.IsSuccess ? Ok(new { success = true, data = ResponseMappers.Dispute(result.Data!) }) : Fail(result);
    }

    /// <summary>Closes without money movement. Resolution is Rejected or ClosedNoAction.</summary>
    [HttpPost("{id:int}/resolve")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolve(int id, [FromBody] DisputeResolveRequest dto)
    {
        var result = await _disputes.ResolveAsync(id, dto.Resolution, dto.AdminNote ?? string.Empty);
        return result.IsSuccess ? Ok(new { success = true, data = ResponseMappers.Dispute(result.Data!) }) : Fail(result);
    }

    /// <summary>
    /// Razorpay refund (resolves as Refunded). Cash-paid requests are refused (422),
    /// as in the MVC admin. 409 if already refunded, 502 if Razorpay fails.
    /// </summary>
    [HttpPost("{id:int}/refund")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    public async Task<IActionResult> Refund(int id, [FromBody] DisputeRefundRequest dto)
    {
        var result = await _disputes.IssueRefundAsync(id, dto.Amount, dto.AdminNote ?? string.Empty);
        return result.IsSuccess
            ? Ok(new { success = true, data = new { razorpayRefundId = result.Data!.RazorpayRefundId, dispute = ResponseMappers.Dispute(result.Data) } })
            : Fail(result);
    }
}
