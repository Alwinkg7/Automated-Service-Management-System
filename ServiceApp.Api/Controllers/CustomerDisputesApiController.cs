using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Api.Contracts;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Api.Controllers;

/// <summary>Customer disputes: raise, list own, detail. Rules live in IDisputeService.</summary>
[Route("api/customer/disputes")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Customer")]
public class CustomerDisputesApiController : ApiControllerBase
{
    private readonly IDisputeService _disputes;

    public CustomerDisputesApiController(IDisputeService disputes) => _disputes = disputes;

    /// <summary>Own disputes, newest first.</summary>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List() =>
        Ok(new { success = true, data = (await _disputes.GetDisputesRaisedByAsync(UserId)).Select(ResponseMappers.Dispute) });

    [HttpGet("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Detail(int id)
    {
        var dispute = await _disputes.GetDisputeDetailsAsync(id);
        if (dispute == null)
            return Fail(StatusCodes.Status404NotFound, "Dispute not found.");
        if (dispute.RaisedByUserId != UserId)
            return Fail(StatusCodes.Status403Forbidden, "This dispute does not belong to you.");

        return Ok(new { success = true, data = ResponseMappers.Dispute(dispute) });
    }

    /// <summary>Raises a dispute on a Completed request owned by the caller.</summary>
    [HttpPost]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> Raise([FromBody] RaiseDisputeRequest dto)
    {
        var result = await _disputes.RaiseDisputeAsync(dto.RequestId, UserId, dto.Reason, dto.Description);
        if (!result.IsSuccess)
            return Fail(result);

        return CreatedAtAction(nameof(Detail), new { id = result.Data!.DisputeId },
            new { success = true, data = new { id = result.Data.DisputeId } });
    }
}
