using Moq;
using ServiceApp.Core.Common;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Services.Implementations;

namespace ServiceApp.Tests;

/// <summary>
/// MVC Customer/Bills/PayCash self-declaration (same risk as BUG-02): choosing
/// cash must not complete the bill; only the assigned technician can.
/// </summary>
public class CashPaymentRegressionTests
{
    private static PaymentService CreateService(TestDb db) => new(
        db.Uow,
        TestDb.Log<PaymentService>(),
        Mock.Of<IRazorpayService>(),
        Mock.Of<INotificationService>(),
        Mock.Of<IEmailService>(),
        new PromotionService(db.Uow, TestDb.Log<PromotionService>()));

    [Fact]
    public async Task Customer_choosing_cash_records_preference_only()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync();

        var result = await CreateService(db).ChooseCashPaymentAsync(s.BillId, s.CustomerId);

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var bill = (await db.Uow.Bills.GetByIdAsync(s.BillId))!;
        Assert.Equal("Cash", bill.PreferredPaymentMethod);
        Assert.Equal(PaymentStatus.Unpaid, bill.PaymentStatus);
        Assert.Equal(RequestStatus.Billed, (await db.Uow.ServiceRequests.GetByIdAsync(s.RequestId))!.Status);
        Assert.Empty(db.Context.Payments);
    }

    [Fact]
    public async Task Another_customer_cannot_choose_cash_for_the_bill()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync();

        var result = await CreateService(db).ChooseCashPaymentAsync(s.BillId, "someone-else");

        Assert.Equal(ErrorType.Forbidden, result.ErrorType);
    }

    [Fact]
    public async Task Customer_cannot_complete_the_job_but_technician_can()
    {
        using var db = new TestDb();
        var s = await db.SeedBilledJobAsync();
        var svc = CreateService(db);
        await svc.ChooseCashPaymentAsync(s.BillId, s.CustomerId);

        var byCustomer = await svc.ConfirmCashCollectedAsync(s.RequestId, s.CustomerId);
        var byTechnician = await svc.ConfirmCashCollectedAsync(s.RequestId, s.TechUserId);

        Assert.False(byCustomer.IsSuccess);
        Assert.True(byTechnician.IsSuccess, byTechnician.ErrorMessage);
        Assert.Equal(RequestStatus.Completed, (await db.Uow.ServiceRequests.GetByIdAsync(s.RequestId))!.Status);
    }

    [Fact]
    public void Self_declared_cash_method_no_longer_exists() =>
        Assert.Null(typeof(IPaymentService).GetMethod("ProcessCashPaymentAsync"));
}
