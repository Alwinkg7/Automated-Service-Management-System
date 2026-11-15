using ServiceApp.Core.Entities;
using ServiceApp.Core.Models.Billing;
using ServiceApp.Core.Models.Reports;

namespace ServiceApp.Api.Contracts;

/// <summary>
/// Entity to JSON shapes shared by several controllers. Field names follow
/// frontend/docs/api-requests.md (see docs/API.md, "Frontend alignment").
/// </summary>
public static class ResponseMappers
{
    public static object Dispute(Dispute d) => new
    {
        id = d.DisputeId,
        requestId = d.RequestId,
        reason = d.Reason,
        description = d.Description,
        status = d.Status,
        resolution = d.Resolution,
        adminNote = d.AdminNote,
        refundAmount = d.RefundAmount,
        refundIssued = d.RefundIssued,
        razorpayRefundId = d.RazorpayRefundId,
        raisedAt = d.RaisedAt,
        resolvedAt = d.ResolvedAt,
        slaDeadline = d.SlaDeadline,
        isSlaBreached = d.IsSlaBreached,
        raisedByName = d.RaisedBy?.FullName,
        category = d.ServiceRequest?.Category.ToString(),
        billAmount = d.ServiceRequest?.Bill?.TotalAmount
    };

    public static object Withdrawal(TechnicianWithdrawal w) => new
    {
        id = w.WithdrawalId,
        amount = w.Amount,
        status = w.Status,
        upiId = w.UpiId,
        adminNote = w.AdminNote,
        requestedAt = w.RequestedAt,
        processedAt = w.ProcessedAt,
        technicianName = w.Technician?.FullName
    };

    public static object Zone(ServiceZone z, int? technicianCount = null) => new
    {
        id = z.ZoneId,
        zoneName = z.ZoneName,
        city = z.City,
        state = z.State,
        isActive = z.IsActive,
        pinCodes = z.GetPinCodeList(),
        technicianCount,
        createdAt = z.CreatedAt
    };

    public static object PromoCode(PromoCode p) => new
    {
        id = p.PromoCodeId,
        code = p.Code,
        description = p.Description,
        discountType = p.DiscountType,
        discountValue = p.DiscountValue,
        maxDiscount = p.MaxDiscount,
        minimumAmount = p.MinimumAmount,
        maxUses = p.MaxUses,
        usedCount = p.UsedCount,
        isActive = p.IsActive,
        isValid = p.IsValid(),
        expiresAt = p.ExpiresAt,
        createdAt = p.CreatedAt
    };

    public static object Technician(TechnicianSummary t, string? zoneName = null) => new
    {
        id = t.TechnicianProfileId,
        userId = t.UserId,
        name = t.FullName,
        email = t.Email,
        phone = t.Phone,
        skill = t.Skill.ToString(),
        status = t.Status.ToString(),
        rating = t.Rating,
        totalJobsCompleted = t.TotalJobsCompleted,
        isActive = t.IsActive,
        zoneId = t.ServiceZoneId,
        zoneName,
        totalEarned = t.TotalEarned,
        activeJobCount = t.ActiveJobCount,
        bio = t.Bio,
        yearsOfExperience = t.YearsOfExperience,
        serviceAreaPinCode = t.ServiceAreaPinCode
    };

    public static object Quote(BillQuote q) => new
    {
        billId = q.BillId,
        subtotal = q.Subtotal,
        discount = q.Discount,
        payable = q.Payable,
        promoCode = q.PromoCode,
        pointsRedeemed = q.PointsRedeemed
    };

    public static object BillItem(BillItem i) => new
    {
        description = i.Description,
        quantity = i.Quantity,
        unitPrice = i.UnitPrice,
        amount = i.Amount
    };

    public static object History(ServiceHistory h) => new
    {
        status = h.Status.ToString(),
        changedAt = h.ChangedAt.ToString("dd MMM yyyy, hh:mm tt"),
        changedAtUtc = h.ChangedAt,
        note = h.Note
    };
}
