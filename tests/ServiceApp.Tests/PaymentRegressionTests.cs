using Moq;
using ServiceApp.Core.Common;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Services.Implementations;

namespace ServiceApp.Tests;

/// <summary>BUG-08 (Razorpay order binding) and BUG-07 (technician cash completion).</summary>
public class PaymentRegressionTests
{
    private static PaymentService CreateService(TestDb db, bool signatureValid = true)
    {
        var razorpay = new Mock<IRazorpayService>();
        razorpay.Setup(r => r.VerifyPaymentSignature(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Returns(signatureValid);

        return new PaymentService(
            db.Uow,
            TestDb.Log<PaymentService>(),
            razorpay.Object,
            Mock.Of<INotificationService>(),
            Mock.Of<IEmailService>(),
            new PromotionService(db.Uow, TestDb.Log<PromotionService>()));
    }

    [Fact]
    public async Task Bug08_Verify_rejects_order_that_is_not_bound_to_the_bill()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync(razorpayOrderId: "order_REAL");

        var result = await CreateService(db).ProcessRazorpayPaymentAsync(
            s.BillId, s.CustomerId, "order_OTHER_CHEAP", "pay_1", "sig");

        Assert.False(result.IsSuccess);
        Assert.Equal(PaymentStatus.Unpaid, (await db.Uow.Bills.GetByIdAsync(s.BillId))!.PaymentStatus);
    }

    [Fact]
    public async Task Bug08_Verify_rejects_when_no_order_was_created_for_the_bill()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync(razorpayOrderId: null);

        var result = await CreateService(db).ProcessRazorpayPaymentAsync(
            s.BillId, s.CustomerId, "order_ANY", "pay_1", "sig");

        Assert.False(result.IsSuccess);
    }

    [Fact]
    public async Task Bug08_Verify_completes_bound_order_once_and_is_idempotent()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync(razorpayOrderId: "order_REAL");
        var svc = CreateService(db);

        var first = await svc.ProcessRazorpayPaymentAsync(s.BillId, s.CustomerId, "order_REAL", "pay_1", "sig");
        var second = await svc.ProcessRazorpayPaymentAsync(s.BillId, s.CustomerId, "order_REAL", "pay_1", "sig");

        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.True(second.IsSuccess, second.ErrorMessage);
        Assert.Equal(first.Data!.PaymentId, second.Data!.PaymentId);
        Assert.True((await db.Uow.Bills.GetByIdAsync(s.BillId))!.IsPaid);
        Assert.Equal(RequestStatus.Completed, (await db.Uow.ServiceRequests.GetByIdAsync(s.RequestId))!.Status);
        Assert.Single(db.Context.Payments);
    }

    [Fact]
    public async Task Bug08_Verify_rejects_invalid_signature()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync(razorpayOrderId: "order_REAL");

        var result = await CreateService(db, signatureValid: false)
            .ProcessRazorpayPaymentAsync(s.BillId, s.CustomerId, "order_REAL", "pay_1", "bad");

        Assert.False(result.IsSuccess);
        Assert.Empty(db.Context.Payments);
    }

    [Fact]
    public async Task Bug08_Webhook_requires_bound_order_and_matching_amount()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync(amount: 850m, razorpayOrderId: "order_REAL");
        var svc = CreateService(db);

        var wrongAmount = await svc.ProcessRazorpayWebhookAsync(new RazorpayWebhookEvent
            { Event = "order.paid", OrderId = "order_REAL", PaymentId = "pay_9", AmountPaise = 100 });
        var unknownOrder = await svc.ProcessRazorpayWebhookAsync(new RazorpayWebhookEvent
            { Event = "order.paid", OrderId = "order_FAKE", PaymentId = "pay_9", AmountPaise = 85000, BillId = s.BillId });
        var ok = await svc.ProcessRazorpayWebhookAsync(new RazorpayWebhookEvent
            { Event = "order.paid", OrderId = "order_REAL", PaymentId = "pay_9", AmountPaise = 85000 });

        Assert.Equal(ErrorType.BusinessRule, wrongAmount.ErrorType);
        Assert.False(unknownOrder.IsSuccess);
        Assert.True(ok.IsSuccess, ok.ErrorMessage);
    }

    [Fact]
    public async Task Bug07_Complete_requires_the_assigned_technician()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync();

        var result = await CreateService(db).ConfirmCashCollectedAsync(s.RequestId, s.OtherTechUserId);

        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
        Assert.Empty(db.Context.Payments);
    }

    [Fact]
    public async Task Bug07_Complete_records_cash_payment_and_completes_job_once()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync();
        var svc = CreateService(db);

        var first = await svc.ConfirmCashCollectedAsync(s.RequestId, s.TechUserId);
        var second = await svc.ConfirmCashCollectedAsync(s.RequestId, s.TechUserId);

        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.Equal("Cash", first.Data!.PaymentMethod);
        Assert.Equal(ErrorType.Conflict, second.ErrorType);
        Assert.Equal(RequestStatus.Completed, (await db.Uow.ServiceRequests.GetByIdAsync(s.RequestId))!.Status);
        Assert.Equal(TechnicianStatus.Available,
            (await db.Uow.TechnicianProfiles.GetByUserIdAsync(s.TechUserId))!.Status);
        Assert.Single(db.Context.Payments);
    }
}
