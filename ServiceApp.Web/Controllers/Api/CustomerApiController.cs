using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using System.Security.Claims;

namespace ServiceApp.Web.Controllers.Api;

[ApiController]
[Route("api/customer")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme,
           Roles = "Customer")]
public class CustomerApiController : ControllerBase
{
    private readonly IServiceRequestService _svc;
    private readonly IBillService _billing;

    public CustomerApiController(
        IServiceRequestService svc, IBillService billing)
    {
        _svc = svc;
        _billing = billing;
    }

    private string UserId =>
        User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    // ── GET /api/customer/dashboard ───────────────────────────────
    [HttpGet("dashboard")]
    public async Task<IActionResult> Dashboard()
    {
        var result = await _svc.GetCustomerRequestsAsync(UserId);
        if (!result.IsSuccess)
            return StatusCode(500,
                new { success = false, error = result.ErrorMessage });

        var list = result.Data!.ToList();

        return Ok(new
        {
            success = true,
            stats = new
            {
                total = list.Count,
                pending = list.Count(r => r.Status == RequestStatus.Pending),
                inProgress = list.Count(r => r.Status == RequestStatus.InProgress),
                completed = list.Count(r => r.Status == RequestStatus.Completed),
            },
            recentRequests = list
                .OrderByDescending(r => r.CreatedAt)
                .Take(5)
                .Select(Summary)
        });
    }

    // ── GET /api/customer/requests?status=Pending ─────────────────
    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests([FromQuery] string? status)
    {
        var result = await _svc.GetCustomerRequestsAsync(UserId);
        if (!result.IsSuccess)
            return StatusCode(500,
                new { success = false, error = result.ErrorMessage });

        var data = result.Data!;
        if (!string.IsNullOrWhiteSpace(status) &&
            Enum.TryParse<RequestStatus>(status, out var s))
            data = data.Where(r => r.Status == s);

        return Ok(new
        {
            success = true,
            data = data.OrderByDescending(r => r.CreatedAt).Select(Summary)
        });
    }

    // ── GET /api/customer/requests/{id} ──────────────────────────
    [HttpGet("requests/{id}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var result = await _svc.GetRequestDetailsAsync(id);
        if (!result.IsSuccess)
            return NotFound(new { success = false, error = result.ErrorMessage });

        var r = result.Data!;
        if (r.CustomerId != UserId) return Forbid();

        return Ok(new
        {
            success = true,
            data = new
            {
                id = r.RequestId,
                category = r.Category.ToString(),
                status = r.Status.ToString(),
                description = r.Description,
                address = r.Address,
                scheduledDate = r.PreferredDateTime.ToString("dd MMM yyyy, hh:mm tt"),
                createdAt = r.CreatedAt.ToString("dd MMM yyyy, hh:mm tt"),
                canCancel = r.Status == RequestStatus.Pending,
                rating = r.CustomerRating,

                technician = r.AssignedTechnician == null ? null : new
                {
                    name = r.AssignedTechnician.User?.FullName,
                    phone = r.AssignedTechnician.User?.Phone,
                    email = r.AssignedTechnician.User?.Email,
                    rating = r.AssignedTechnician.Rating,
                },

                bill = r.Bill == null ? null : new
                {
                    id = r.Bill.Id,
                    amount = r.Bill.TotalAmount,
                    laborCost = r.Bill.LaborCost,
                    materialCost = r.Bill.MaterialCost,
                    description = r.Bill.Description,
                    isPaid = r.Bill.IsPaid,
                    createdAt = r.Bill.CreatedAt.ToString("dd MMM yyyy"),
                },

                statusHistory = r.History
                    .OrderBy(h => h.ChangedAt)
                    .Select(h => new
                    {
                        status = h.Status.ToString(),
                        changedAt = h.ChangedAt.ToString("dd MMM yyyy, hh:mm tt"),
                        note = h.Note,
                    }),
            }
        });
    }

    // ── GET /api/customer/categories ─────────────────────────────
    [HttpGet("categories")]
    public IActionResult GetCategories() =>
        Ok(new
        {
            success = true,
            data = Enum.GetValues<ServiceCategory>().Select(c => new
            {
                value = c.ToString(),
                label = c.ToString().Replace("_", " "),
                description = CategoryDesc(c),
                icon = CategoryIcon(c),
            })
        });

    // ── POST /api/customer/requests ───────────────────────────────
    [HttpPost("requests")]
    public async Task<IActionResult> Create([FromBody] CreateRequestDto dto)
    {
        if (!Enum.TryParse<ServiceCategory>(dto.Category, out var cat))
            return BadRequest(new { success = false, error = "Invalid category." });

        var result = await _svc.CreateRequestAsync(
            UserId,
            dto.Description,
            cat,
            dto.Address,
            dto.PinCode ?? string.Empty,
            dto.ScheduledDate);

        if (!result.IsSuccess)
            return BadRequest(new { success = false, error = result.ErrorMessage });

        return Ok(new { success = true, requestId = result.Data!.RequestId });
    }

    // ── POST /api/customer/requests/{id}/cancel ───────────────────
    [HttpPost("requests/{id}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        var check = await _svc.GetRequestDetailsAsync(id);
        if (!check.IsSuccess)
            return NotFound(new { success = false, error = check.ErrorMessage });
        if (check.Data!.CustomerId != UserId) return Forbid();

        var result = await _svc.CancelRequestAsync(id, UserId);
        if (!result.IsSuccess)
            return BadRequest(new { success = false, error = result.ErrorMessage });

        return Ok(new { success = true });
    }

    // ── POST /api/customer/requests/{id}/rate ─────────────────────
    [HttpPost("requests/{id}/rate")]
    public async Task<IActionResult> Rate(int id, [FromBody] RateDto dto)
    {
        var check = await _svc.GetRequestDetailsAsync(id);
        if (!check.IsSuccess)
            return NotFound(new { success = false, error = check.ErrorMessage });
        if (check.Data!.CustomerId != UserId) return Forbid();

        var result = await _svc.SubmitRatingAsync(
            id, UserId, dto.Stars, dto.Comment);

        if (!result.IsSuccess)
            return BadRequest(new { success = false, error = result.ErrorMessage });

        return Ok(new { success = true });
    }

    [HttpPost("bills/{id}/pay")]
    public async Task<IActionResult> PayBill(int id)
    {
        var result = await _billing.PayBillAsync(id, UserId);
        if (!result.IsSuccess)
            return BadRequest(new { success = false, error = result.ErrorMessage });
        return Ok(new { success = true });
    }

    // ── Helpers ───────────────────────────────────────────────────
    private static object Summary(Core.Entities.ServiceRequest r) => new
    {
        id = r.RequestId,
        category = r.Category.ToString(),
        status = r.Status.ToString(),
        description = r.Description,
        scheduledDate = r.PreferredDateTime.ToString("dd MMM yyyy"),
        createdAt = r.CreatedAt.ToString("dd MMM yyyy"),
        technicianName = r.AssignedTechnician?.User?.FullName,
        hasBill = r.Bill != null,
        billPaid = r.Bill?.IsPaid ?? false,
        billAmount = r.Bill?.TotalAmount,
    };

    private static string CategoryDesc(ServiceCategory c) => c switch
    {
        ServiceCategory.Plumbing => "Pipes, leaks, taps & drainage",
        ServiceCategory.Electrical => "Wiring, switches & power issues",
        ServiceCategory.Carpentry => "Furniture, doors & woodwork",
        ServiceCategory.Cleaning => "Deep clean, pest control",
        ServiceCategory.AC_Repair => "AC servicing & installation",
        ServiceCategory.Appliance_Repair => "Washing machine, fridge & more",
        ServiceCategory.Painting => "Interior & exterior painting",
        _ => "General home services",
    };

    private static string CategoryIcon(ServiceCategory c) => c switch
    {
        ServiceCategory.Plumbing => "plumbing",
        ServiceCategory.Electrical => "electrical_services",
        ServiceCategory.Carpentry => "carpenter",
        ServiceCategory.Cleaning => "cleaning_services",
        ServiceCategory.AC_Repair => "ac_unit",
        ServiceCategory.Appliance_Repair => "kitchen",
        ServiceCategory.Painting => "format_paint",
        _ => "home_repair_service",
    };
}

public record CreateRequestDto(
    string Category,
    string Description,
    DateTime ScheduledDate,
    string Address,
    string? PinCode);

public record RateDto(int Stars, string? Comment);