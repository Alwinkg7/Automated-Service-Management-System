using Microsoft.Extensions.Configuration;
using Moq;
using ServiceApp.Api.Contracts;
using ServiceApp.Api.Controllers;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Services.Implementations;

namespace ServiceApp.Tests;

/// <summary>Customer and admin dispute endpoints (validation, ownership, state rules, cash refund refusal).</summary>
public class DisputeEndpointTests
{
    private const string Description = "The tap still leaks after the visit.";

    private static DisputeService CreateService(TestDb db) => new(
        db.Uow,
        Mock.Of<IRazorpayService>(),
        Mock.Of<IEmailService>(),
        new ConfigurationBuilder().Build(),
        TestDb.Log<DisputeService>());

    /// <summary>Completes the seeded job with a cash payment.</summary>
    private static async Task<Scenario> ArrangeCompletedCashJobAsync(TestDb db)
    {
        var s = await db.SeedBilledJobAsync(700m);
        (await db.Uow.ServiceRequests.GetByIdAsync(s.RequestId))!.Status = RequestStatus.Completed;
        var bill = (await db.Uow.Bills.GetByIdAsync(s.BillId))!;
        bill.PaymentStatus = PaymentStatus.Paid;
        db.Context.Payments.Add(new Payment
        {
            BillId = s.BillId, Amount = 700m, PaymentMethod = "Cash", GatewayTransactionId = $"CASH-{s.BillId}-1"
        });
        await db.Context.SaveChangesAsync();
        return s;
    }

    [Fact]
    public async Task Raise_validates_reason_and_request_state()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync(); // still Billed
        var controller = new CustomerDisputesApiController(CreateService(db)).As(s.CustomerId, "Customer");

        var badReason = await controller.Raise(new RaiseDisputeRequest(s.RequestId, "Bogus", Description));
        var shortText = await controller.Raise(new RaiseDisputeRequest(s.RequestId, "ServiceQuality", "too short"));
        var notCompleted = await controller.Raise(new RaiseDisputeRequest(s.RequestId, "ServiceQuality", Description));

        Assert.Equal(400, badReason.Status());
        Assert.Equal(400, shortText.Status());
        Assert.Equal(422, notCompleted.Status());
    }

    [Fact]
    public async Task Raise_list_detail_and_duplicate_rules()
    {
        using var db = new TestDb();
        var s = await ArrangeCompletedCashJobAsync(db);
        var svc = CreateService(db);
        var controller = new CustomerDisputesApiController(svc).As(s.CustomerId, "Customer");

        var created = await controller.Raise(new RaiseDisputeRequest(s.RequestId, "ServiceQuality", Description));
        var duplicate = await controller.Raise(new RaiseDisputeRequest(s.RequestId, "WrongCharge", Description));
        var dispute = Assert.Single(await svc.GetDisputesRaisedByAsync(s.CustomerId));
        var otherCustomer = new CustomerDisputesApiController(svc).As("someone-else", "Customer");

        Assert.Equal(201, created.Status());
        Assert.Equal(409, duplicate.Status());
        Assert.Equal(200, (await controller.Detail(dispute.DisputeId)).Status());
        Assert.Equal(403, (await otherCustomer.Detail(dispute.DisputeId)).Status());
        Assert.Equal(404, (await controller.Detail(9999)).Status());
    }

    [Fact]
    public async Task Admin_review_resolve_and_cash_refund_refusal()
    {
        using var db = new TestDb();
        var s = await ArrangeCompletedCashJobAsync(db);
        var svc = CreateService(db);
        var id = (await svc.RaiseDisputeAsync(s.RequestId, s.CustomerId, "ServiceQuality", Description)).Data!.DisputeId;
        var admin = new AdminDisputesApiController(svc).As("admin-1", "Admin");

        Assert.Equal(200, (await admin.Review(id, new DisputeReviewRequest("checking"))).Status());
        Assert.Equal(409, (await admin.Review(id, new DisputeReviewRequest("again"))).Status());
        Assert.Equal(422, (await admin.Refund(id, new DisputeRefundRequest(100m, "partial"))).Status());
        Assert.Equal(400, (await admin.Resolve(id, new DisputeResolveRequest("Refunded", "x"))).Status());
        Assert.Equal(200, (await admin.Resolve(id, new DisputeResolveRequest("ClosedNoAction", "fixed"))).Status());
        Assert.Equal(409, (await admin.Resolve(id, new DisputeResolveRequest("Rejected", "x"))).Status());
    }
}
