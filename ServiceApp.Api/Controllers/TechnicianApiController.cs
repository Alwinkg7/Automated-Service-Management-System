using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Api.Contracts;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Api.Controllers;

/// <summary>Technician endpoints: availability, jobs and billing.</summary>
[Route("api/technician")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Technician")]
public class TechnicianApiController : ApiControllerBase
{
    private readonly IServiceRequestService _svc;
    private readonly ITechnicianService _techSvc;
    private readonly IBillService _billing;
    private readonly IPaymentService _payments;

    public TechnicianApiController(
        IServiceRequestService svc,
        ITechnicianService techSvc,
        IBillService billing,
        IPaymentService payments)
    {
        _svc = svc;
        _techSvc = techSvc;
        _billing = billing;
        _payments = payments;
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Dashboard()
    {
        var techResult = await _techSvc.GetByUserIdAsync(UserId);
        if (!techResult.IsSuccess)
            return Fail(StatusCodes.Status404NotFound, techResult.ErrorMessage);

        var tech = techResult.Data!;
        var jobsResult = await _svc.GetTechnicianJobsAsync(tech.TechnicianProfileId);
        var jobs = jobsResult.IsSuccess ? jobsResult.Data!.ToList() : new List<ServiceRequest>();

        var active = jobs.FirstOrDefault(j =>
            j.Status == RequestStatus.Assigned || j.Status == RequestStatus.InProgress);

        return Ok(new
        {
            success = true,
            technician = new
            {
                name = tech.User?.FullName,
                status = tech.Status.ToString(),
                rating = tech.Rating,
                totalJobs = jobs.Count,
                completedJobs = jobs.Count(j => j.Status == RequestStatus.Completed),
                pendingBills = jobs.Count(j => j.Status == RequestStatus.Billed),
            },
            activeJob = active == null ? null : new
            {
                id = active.RequestId,
                category = active.Category.ToString(),
                status = active.Status.ToString(),
                customerName = active.Customer?.FullName,
                address = active.Address,
                scheduledDate = active.PreferredDateTime.ToString("dd MMM yyyy, hh:mm tt"),
            },
            recentJobs = jobs
                .Where(j => j.Status == RequestStatus.Completed)
                .OrderByDescending(j => j.UpdatedAt)
                .Take(3)
                .Select(j => new
                {
                    id = j.RequestId,
                    category = j.Category.ToString(),
                    customerName = j.Customer?.FullName,
                    completedAt = j.UpdatedAt?.ToString("dd MMM yyyy"),
                    billPaid = IsPaid(j.Bill),
                }),
        });
    }

    /// <summary>Toggles Available/Offline. Fails while Busy.</summary>
    [HttpPost("toggle-status")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ToggleStatus()
    {
        var result = await _techSvc.ToggleAvailabilityAsync(UserId);
        if (!result.IsSuccess)
            return Fail(result);

        return Ok(new { success = true, newStatus = result.Data!.ToString() });
    }

    [HttpGet("jobs")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetJobs([FromQuery] string? status)
    {
        var techResult = await _techSvc.GetByUserIdAsync(UserId);
        if (!techResult.IsSuccess)
            return Fail(StatusCodes.Status404NotFound, techResult.ErrorMessage);

        var result = await _svc.GetTechnicianJobsAsync(techResult.Data!.TechnicianProfileId);
        if (!result.IsSuccess)
            return Fail(StatusCodes.Status500InternalServerError, result.ErrorMessage);

        var jobs = result.Data!;
        if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<RequestStatus>(status, true, out var s))
            jobs = jobs.Where(j => j.Status == s);

        return Ok(new
        {
            success = true,
            data = jobs.OrderByDescending(j => j.CreatedAt).Select(j => new
            {
                id = j.RequestId,
                category = j.Category.ToString(),
                status = j.Status.ToString(),
                description = j.Description,
                address = j.Address,
                scheduledDate = j.PreferredDateTime.ToString("dd MMM yyyy, hh:mm tt"),
                customerName = j.Customer?.FullName,
                customerPhone = j.Customer?.Phone,
                hasBill = j.Bill != null,
                billPaid = IsPaid(j.Bill),
            })
        });
    }

    [HttpGet("jobs/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(int id)
    {
        var techResult = await _techSvc.GetByUserIdAsync(UserId);
        if (!techResult.IsSuccess)
            return Fail(StatusCodes.Status404NotFound, techResult.ErrorMessage);

        var result = await _svc.GetRequestDetailsAsync(id);
        if (!result.IsSuccess)
            return Fail(StatusCodes.Status404NotFound, result.ErrorMessage);

        var j = result.Data!;
        if (j.AssignedTechnicianProfileId != techResult.Data!.TechnicianProfileId)
            return Fail(StatusCodes.Status403Forbidden, "This job is not assigned to you.");

        return Ok(new
        {
            success = true,
            data = new
            {
                id = j.RequestId,
                category = j.Category.ToString(),
                status = j.Status.ToString(),
                description = j.Description,
                address = j.Address,
                scheduledDate = j.PreferredDateTime.ToString("dd MMM yyyy, hh:mm tt"),
                createdAt = j.CreatedAt.ToString("dd MMM yyyy"),
                canAccept = j.Status == RequestStatus.Assigned,
                canBill = j.Status == RequestStatus.InProgress,
                canComplete = j.Status == RequestStatus.Billed,

                customer = new
                {
                    name = j.Customer?.FullName,
                    phone = j.Customer?.Phone,
                    email = j.Customer?.Email,
                },

                bill = j.Bill == null ? null : new
                {
                    amount = j.Bill.TotalAmount,
                    laborCost = j.Bill.LaborCost,
                    materialCost = j.Bill.MaterialCost,
                    description = j.Bill.Description,
                    isPaid = IsPaid(j.Bill),
                },
            }
        });
    }

    [HttpPost("jobs/{id:int}/accept")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Accept(int id)
    {
        var result = await _svc.AcceptJobAsync(id, UserId);
        if (!result.IsSuccess)
            return Fail(result);

        return Ok(new { success = true });
    }

    /// <summary>Declines an Assigned job; it returns to Pending for re-assignment.</summary>
    [HttpPost("jobs/{id:int}/reject")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reject(int id)
    {
        var result = await _svc.RejectJobAsync(id, UserId);
        if (!result.IsSuccess)
            return Fail(result);

        return Ok(new { success = true });
    }

    /// <summary>
    /// The assigned technician confirms cash was collected for a Billed job:
    /// records a Cash payment and moves the request to Completed.
    /// </summary>
    [HttpPost("jobs/{id:int}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(int id)
    {
        var result = await _payments.ConfirmCashCollectedAsync(id, UserId);
        if (!result.IsSuccess)
            return Fail(result);

        return Ok(new { success = true, data = new { paymentId = result.Data!.PaymentId } });
    }

    /// <summary>Creates the bill from flat labour/material costs (converted to line items).</summary>
    [HttpPost("jobs/{id:int}/bill")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> CreateBill(int id, [FromBody] CreateBillDto dto)
    {
        var items = new List<BillItemInput>();

        if (dto.LaborCost > 0)
            items.Add(new BillItemInput { Description = "Labour charges", Quantity = 1, UnitPrice = dto.LaborCost });

        if (dto.MaterialCost > 0)
            items.Add(new BillItemInput
            {
                Description = string.IsNullOrWhiteSpace(dto.Description) ? "Materials & parts" : dto.Description,
                Quantity = 1,
                UnitPrice = dto.MaterialCost
            });

        if (items.Count == 0)
            return Fail(StatusCodes.Status400BadRequest, "Enter at least one cost (labour or materials).");

        var result = await _billing.CreateBillAsync(id, UserId, items);
        if (!result.IsSuccess)
            return Fail(result);

        return Ok(new { success = true, billId = result.Data!.Id });
    }

    private static bool IsPaid(Bill? bill) =>
        bill != null && (bill.IsPaid || bill.PaymentStatus == PaymentStatus.Paid);
}
