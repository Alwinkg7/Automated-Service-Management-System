using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using System.Security.Claims;

namespace ServiceApp.Web.Controllers.Api;

[ApiController]
[Route("api/technician")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
           Roles = "Technician")]
public class TechnicianApiController : ControllerBase
{
    private readonly IServiceRequestService _svc;
    private readonly ITechnicianService _techSvc;
    private readonly IBillService _billing;

    public TechnicianApiController(
        IServiceRequestService svc,
        ITechnicianService techSvc,
        IBillService billing)
    {
        _svc = svc;
        _techSvc = techSvc;
        _billing = billing;
    }

    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ── GET /api/technician/dashboard ─────────────────────────────
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var techResult = await _techSvc.GetByUserIdAsync(UserId);
        if (!techResult.IsSuccess)
            return NotFound(new { success = false, error = techResult.ErrorMessage });

        var tech = techResult.Data!;
        var jobsResult = await _svc.GetTechnicianJobsAsync(tech.TechnicianProfileId);
        var jobs = jobsResult.IsSuccess ? jobsResult.Data!.ToList() : new();

        var active = jobs.FirstOrDefault(j =>
            j.Status == RequestStatus.Assigned ||
            j.Status == RequestStatus.InProgress);

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
                    billPaid = j.Bill?.IsPaid ?? false,
                }),
        });
    }

    // ── POST /api/technician/toggle-status ────────────────────────
    [HttpPost("toggle-status")]
    public async Task<IActionResult> ToggleStatus()
    {
        var result = await _techSvc.ToggleAvailabilityAsync(UserId);
        if (!result.IsSuccess)
            return BadRequest(new { success = false, error = result.ErrorMessage });
        return Ok(new { success = true, newStatus = result.Data!.ToString() });
    }

    // ── GET /api/technician/jobs ──────────────────────────────────
    [HttpGet("jobs")]
    public async Task<IActionResult> GetJobs([FromQuery] string? status)
    {
        var techResult = await _techSvc.GetByUserIdAsync(UserId);
        if (!techResult.IsSuccess)
            return NotFound(new { success = false, error = techResult.ErrorMessage });

        var result = await _svc.GetTechnicianJobsAsync(
                         techResult.Data!.TechnicianProfileId);
        if (!result.IsSuccess)
            return StatusCode(500,
                new { success = false, error = result.ErrorMessage });

        var jobs = result.Data!;
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<RequestStatus>(status, out var s))
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
                billPaid = j.Bill?.IsPaid ?? false,
            })
        });
    }

    // ── GET /api/technician/jobs/{id} ─────────────────────────────
    [HttpGet("jobs/{id}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var techResult = await _techSvc.GetByUserIdAsync(UserId);
        if (!techResult.IsSuccess)
            return NotFound(new { success = false, error = techResult.ErrorMessage });

        var result = await _svc.GetRequestDetailsAsync(id);
        if (!result.IsSuccess)
            return NotFound(new { success = false, error = result.ErrorMessage });

        var j = result.Data!;
        if (j.AssignedTechnicianProfileId != techResult.Data!.TechnicianProfileId)
            return Forbid();

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
                    isPaid = j.Bill.IsPaid,
                },
            }
        });
    }

    // ── POST /api/technician/jobs/{id}/accept ─────────────────────
    [HttpPost("jobs/{id}/accept")]
    public async Task<IActionResult> Accept(int id)
    {
        var result = await _svc.AcceptJobAsync(id, UserId);
        if (!result.IsSuccess)
            return BadRequest(new { success = false, error = result.ErrorMessage });
        return Ok(new { success = true });
    }

    // ── POST /api/technician/jobs/{id}/complete ───────────────────
    [HttpPost("jobs/{id}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        var result = await _svc.GetRequestDetailsAsync(id);
        if (!result.IsSuccess)
            return NotFound(new { success = false, error = result.ErrorMessage });
        return Ok(new { success = true });
    }

    // ── POST /api/technician/jobs/{id}/bill ───────────────────────────
    [HttpPost("jobs/{id}/bill")]
    public async Task<IActionResult> CreateBill(
        int id, [FromBody] CreateBillDto dto)
    {
        var techResult = await _techSvc.GetByUserIdAsync(UserId);
        if (!techResult.IsSuccess)
            return NotFound(new
            {
                success = false,
                error = techResult.ErrorMessage
            });

        // ── Convert Flutter's flat DTO → BillItemInput list ──────────
        // Flutter sends separate laborCost + materialCost fields.
        // BillService expects a list of line items.
        var items = new List<BillItemInput>();

        if (dto.LaborCost > 0)
            items.Add(new BillItemInput
            {
                Description = "Labour charges",
                Quantity = 1,
                UnitPrice = dto.LaborCost
            });

        if (dto.MaterialCost > 0)
            items.Add(new BillItemInput
            {
                Description = string.IsNullOrWhiteSpace(dto.Description)
                    ? "Materials & parts"
                    : dto.Description,
                Quantity = 1,
                UnitPrice = dto.MaterialCost
            });

        if (!items.Any())
            return BadRequest(new
            {
                success = false,
                error = "Enter at least one cost (labour or materials)."
            });

        // ── CreateBillAsync takes technicianUserId (string) ──────────
        var result = await _billing.CreateBillAsync(
            id,
            UserId,       // string technicianUserId — matches service signature
            items);

        if (!result.IsSuccess)
            return BadRequest(new
            {
                success = false,
                error = result.ErrorMessage
            });

        return Ok(new { success = true, billId = result.Data!.Id });
    }
}

public record CreateBillDto(
    decimal LaborCost,
    decimal MaterialCost,
    string? Description);