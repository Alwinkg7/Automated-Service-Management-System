using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ServiceApp.Api.Contracts;
using ServiceApp.Api.Extensions;
using ServiceApp.Api.Infrastructure;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Api.Controllers;

/// <summary>Customer endpoints: service requests, ratings, bills and payments.</summary>
[Route("api/customer")]
[Authorize(AuthenticationSchemes = JwtBearerDefaults.AuthenticationScheme, Roles = "Customer")]
public class CustomerApiController : ApiControllerBase
{
    private readonly IServiceRequestService _svc;
    private readonly IBillService _billing;
    private readonly IPaymentService _payments;
    private readonly IBillPdfService _pdf;
    private readonly IRequestQueries _queries;
    private readonly IBillDiscountService _discounts;
    private readonly RazorpaySettings _razorpay;

    public CustomerApiController(
        IServiceRequestService svc,
        IBillService billing,
        IPaymentService payments,
        IBillPdfService pdf,
        IRequestQueries queries,
        IBillDiscountService discounts,
        IOptions<RazorpaySettings> razorpay)
    {
        _svc = svc;
        _billing = billing;
        _payments = payments;
        _pdf = pdf;
        _queries = queries;
        _discounts = discounts;
        _razorpay = razorpay.Value;
    }

    // ── Requests ──────────────────────────────────────────────────────

    [HttpGet("dashboard")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard()
    {
        var result = await _svc.GetCustomerRequestsAsync(UserId);
        if (!result.IsSuccess)
            return Fail(StatusCodes.Status500InternalServerError, result.ErrorMessage);

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

    /// <summary>Lists the caller's requests, optionally filtered by status name (e.g. <c>Pending</c>).</summary>
    [HttpGet("requests")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetRequests([FromQuery] string? status, CancellationToken ct)
    {
        // Filtered in SQL; the legacy response is the full (unpaged) list.
        var page = await _queries.SearchAsync(new Core.Models.Queries.RequestSearchCriteria
        {
            CustomerId = UserId,
            Statuses = Enum.TryParse<RequestStatus>(status, true, out var s) ? [s] : null,
            PageSize = 0
        }, ct);

        return Ok(new
        {
            success = true,
            data = page.Items.Select(Summary)
        });
    }

    [HttpGet("requests/{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetDetail(int id)
    {
        var result = await _svc.GetRequestDetailsAsync(id);
        if (!result.IsSuccess)
            return Fail(StatusCodes.Status404NotFound, result.ErrorMessage);

        var r = result.Data!;
        if (r.CustomerId != UserId)
            return Fail(StatusCodes.Status403Forbidden, "You can only view your own requests.");

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
                canCancel = r.Status is RequestStatus.Pending or RequestStatus.Assigned,
                rating = r.CustomerRating,
                feedback = r.CustomerFeedback,
                pinCode = r.PinCode,
                canRebook = r.Status is RequestStatus.Completed or RequestStatus.Cancelled,

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
                    isPaid = IsPaid(r.Bill),
                    createdAt = r.Bill.CreatedAt.ToString("dd MMM yyyy"),
                    paidAt = r.Bill.PaidAt,
                    discount = r.Bill.DiscountAmount,
                    payable = r.Bill.PayableAmount,
                    promoCode = r.Bill.PromoCode,
                    pointsRedeemed = r.Bill.PointsRedeemed,
                    preferredPaymentMethod = r.Bill.PreferredPaymentMethod,
                    paymentMethod = r.Bill.Payment?.PaymentMethod,
                    items = r.Bill.BillItems.Select(ResponseMappers.BillItem),
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

    [HttpGet("categories")]
    [AllowAnonymous]
    [ProducesResponseType(StatusCodes.Status200OK)]
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

    [HttpPost("requests")]
    [Idempotent]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ValidationProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateRequestDto dto)
    {
        if (!Enum.TryParse<ServiceCategory>(dto.Category, true, out var cat))
            return Fail(StatusCodes.Status400BadRequest, "Invalid category.");

        var result = await _svc.CreateRequestAsync(
            UserId,
            dto.Description,
            cat,
            dto.Address,
            dto.PinCode ?? string.Empty,
            dto.ScheduledDate);

        if (!result.IsSuccess)
            return Fail(result);

        // 200 (not 201) is kept for compatibility with existing mobile clients.
        return Ok(new { success = true, requestId = result.Data!.RequestId });
    }

    [HttpPost("requests/{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cancel(int id)
    {
        var ownership = await EnsureOwnRequestAsync(id);
        if (ownership != null) return ownership;

        var result = await _svc.CancelRequestAsync(id, UserId);
        if (!result.IsSuccess)
            return Fail(result);

        return Ok(new { success = true });
    }

    [HttpPost("requests/{id:int}/rate")]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Rate(int id, [FromBody] RateDto dto)
    {
        var ownership = await EnsureOwnRequestAsync(id);
        if (ownership != null) return ownership;

        var result = await _svc.SubmitRatingAsync(id, UserId, dto.Stars, dto.Comment);
        if (!result.IsSuccess)
            return Fail(result);

        return Ok(new { success = true });
    }

    // ── Bills & payments ──────────────────────────────────────────────

    /// <summary>
    /// Removed: it marked bills paid without any payment. Use razorpay/order +
    /// razorpay/verify; cash is confirmed by the technician (jobs/{id}/complete).
    /// </summary>
    [HttpPost("bills/{id:int}/pay")]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status410Gone)]
    public IActionResult PayBill(int id) =>
        Fail(StatusCodes.Status410Gone,
            "This endpoint was removed. Pay via POST /api/customer/bills/{id}/razorpay/order and /razorpay/verify.");

    /// <summary>Creates a Razorpay order for the bill; the amount comes from the server-side bill.</summary>
    [HttpPost("bills/{id:int}/razorpay/order")]
    [EnableRateLimiting(RateLimitPolicies.Payments)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status502BadGateway)]
    [Idempotent]
    public async Task<IActionResult> CreateRazorpayOrder(
        int id, [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RazorpayOrderRequest? body)
    {
        // Optional checkout discounts are applied (idempotently) before the order is
        // created, so the order amount is the discounted payable amount.
        var quote = await _discounts.PrepareCheckoutAsync(id, UserId, body?.PromoCode, body?.RedeemPoints == true);
        if (!quote.IsSuccess)
            return Fail(quote);

        var result = await _payments.CreateRazorpayOrderAsync(id, UserId);
        if (!result.IsSuccess)
            return Fail(result);

        var order = result.Data!;
        var q = quote.Data!;
        return Ok(new
        {
            success = true,
            data = new
            {
                billId = id,
                orderId = order.OrderId,
                amount = order.Amount,
                currency = order.Currency,
                keyId = _razorpay.KeyId,
                subtotal = q.Subtotal,
                discount = q.Discount,
                payable = q.Payable,
                promoCode = q.PromoCode,
                pointsRedeemed = q.PointsRedeemed
            }
        });
    }

    /// <summary>Verifies the Razorpay checkout signature and completes the bill. Idempotent.</summary>
    [HttpPost("bills/{id:int}/razorpay/verify")]
    [EnableRateLimiting(RateLimitPolicies.Payments)]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> VerifyRazorpayPayment(int id, [FromBody] VerifyRazorpayPaymentRequest dto)
    {
        var result = await _payments.ProcessRazorpayPaymentAsync(
            id, UserId, dto.RazorpayOrderId, dto.RazorpayPaymentId, dto.RazorpaySignature);
        if (!result.IsSuccess)
            return Fail(result);

        var payment = result.Data!;
        return Ok(new
        {
            success = true,
            data = new
            {
                paymentId = payment.PaymentId,
                billId = payment.BillId,
                amount = payment.Amount,
                method = payment.PaymentMethod,
                transactionId = payment.GatewayTransactionId,
                paidAt = payment.PaidAt
            }
        });
    }

    /// <summary>Downloads the bill as a PDF invoice.</summary>
    [HttpGet("bills/{id:int}/pdf")]
    [Produces("application/pdf", "application/problem+json")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DownloadBillPdf(int id)
    {
        var bill = await _billing.GetBillByIdAsync(id);
        if (!bill.IsSuccess)
            return Fail(StatusCodes.Status404NotFound, bill.ErrorMessage);

        var ownership = await EnsureOwnRequestAsync(bill.Data!.ServiceRequestId);
        if (ownership != null) return ownership;

        var bytes = await _pdf.GenerateBillPdfAsync(id);
        return File(bytes, "application/pdf", $"bill-{id}.pdf");
    }

    /// <summary>
    /// Customer chooses to pay in cash. Nothing is marked paid: the assigned
    /// technician completes the job with POST /api/technician/jobs/{id}/complete.
    /// </summary>
    [HttpPost("bills/{id:int}/cash")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChooseCash(int id)
    {
        var result = await _payments.ChooseCashPaymentAsync(id, UserId);
        if (!result.IsSuccess)
            return Fail(result);

        return Ok(new
        {
            success = true,
            data = new
            {
                billId = id,
                preferredPaymentMethod = result.Data!.PreferredPaymentMethod,
                payable = result.Data.PayableAmount,
                message = "Pay the technician in cash; the job completes when they confirm receipt."
            }
        });
    }

    // ── History & re-booking ──────────────────────────────────────────

    /// <summary>Completed and Cancelled requests with totals; search and category filter run in SQL.</summary>
    [HttpGet("history")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> History([FromQuery] string? search, [FromQuery] string? category, CancellationToken ct)
    {
        var page = await _queries.SearchAsync(new Core.Models.Queries.RequestSearchCriteria
        {
            CustomerId = UserId,
            Statuses = [RequestStatus.Completed, RequestStatus.Cancelled],
            Category = Enum.TryParse<ServiceCategory>(category, true, out var c) ? c : null,
            Search = search,
            PageSize = 0
        }, ct);
        var summary = await _queries.GetCustomerHistorySummaryAsync(UserId, ct);

        return Ok(new
        {
            success = true,
            data = new
            {
                totalJobs = summary.TotalJobs,
                completedJobs = summary.CompletedJobs,
                cancelledJobs = summary.CancelledJobs,
                totalSpent = summary.TotalSpent,
                averageRating = Math.Round(summary.AverageRating, 2),
                items = page.Items.Select(Summary)
            }
        });
    }

    /// <summary>Creates a new Pending request from an own Completed or Cancelled request.</summary>
    [HttpPost("requests/{id:int}/rebook")]
    [Idempotent]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Rebook(int id, [FromBody] RebookRequest dto)
    {
        var result = await _svc.RebookAsync(id, UserId, dto.ScheduledDate, dto.Description, dto.Address);
        if (!result.IsSuccess)
            return Fail(result);

        return CreatedAtAction(nameof(GetDetail), new { id = result.Data!.RequestId },
            new { success = true, requestId = result.Data.RequestId });
    }

    // ── Helpers ───────────────────────────────────────────────────────

    /// <summary>Returns 404/403 when the request is missing or not owned by the caller; null when OK.</summary>
    private async Task<IActionResult?> EnsureOwnRequestAsync(int requestId)
    {
        var check = await _svc.GetRequestDetailsAsync(requestId);
        if (!check.IsSuccess)
            return Fail(StatusCodes.Status404NotFound, check.ErrorMessage);
        if (check.Data!.CustomerId != UserId)
            return Fail(StatusCodes.Status403Forbidden, "This request does not belong to you.");
        return null;
    }

    private static bool IsPaid(Bill bill) => bill.IsPaid;

    private static object Summary(ServiceRequest r) => new
    {
        id = r.RequestId,
        category = r.Category.ToString(),
        status = r.Status.ToString(),
        description = r.Description,
        scheduledDate = r.PreferredDateTime.ToString("dd MMM yyyy"),
        createdAt = r.CreatedAt.ToString("dd MMM yyyy"),
        technicianName = r.AssignedTechnician?.User?.FullName,
        hasBill = r.Bill != null,
        billPaid = r.Bill != null && IsPaid(r.Bill),
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
