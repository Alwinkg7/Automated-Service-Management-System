using Microsoft.AspNetCore.Mvc;
using ServiceApp.Api.Contracts;
using ServiceApp.Api.Controllers;
using ServiceApp.Core.Enums;
using ServiceApp.Services.Implementations;

namespace ServiceApp.Tests;

/// <summary>Technician withdrawal and admin processing endpoints (WalletService state machine).</summary>
public class WithdrawalEndpointTests
{
    private static async Task<(TestDb Db, WalletService Wallet, Scenario S)> ArrangeAsync()
    {
        var db = new TestDb();
        var s = await db.SeedBilledJobAsync(1000m);
        (await db.Uow.ServiceRequests.GetByIdAsync(s.RequestId))!.Status = RequestStatus.Completed;
        var bill = (await db.Uow.Bills.GetByIdAsync(s.BillId))!;
        bill.PaymentStatus = PaymentStatus.Paid;
        bill.PaidAt = DateTime.UtcNow;
        await db.Context.SaveChangesAsync();
        return (db, new WalletService(db.Uow, TestDb.Log<WalletService>()), s);
    }

    [Fact]
    public async Task Withdrawal_over_server_balance_is_422_and_valid_one_is_201()
    {
        var (db, wallet, s) = await ArrangeAsync();
        using var _ = db;
        var controller = new TechnicianWalletApiController(wallet).As(s.TechUserId, "Technician");

        var tooMuch = await controller.RequestWithdrawal(new WithdrawalRequestDto(881m, "tech@upi"));
        var ok = await controller.RequestWithdrawal(new WithdrawalRequestDto(880m, "tech@upi"));

        Assert.Equal(422, tooMuch.Status());
        Assert.Equal(201, ok.Status());
        Assert.IsAssignableFrom<ProblemDetails>(((ObjectResult)tooMuch).Value);
    }

    [Fact]
    public async Task Wallet_summary_reports_net_balance()
    {
        var (db, wallet, s) = await ArrangeAsync();
        using var _ = db;

        var summary = await wallet.GetSummaryAsync(s.TechUserId);

        Assert.Equal(1000m, summary.GrossEarningsAllTime);
        Assert.Equal(880m, summary.AvailableToWithdraw);
        Assert.Equal(6, summary.GrossLast6Months.Count);
        Assert.Single(await wallet.GetPayoutHistoryAsync(s.TechUserId));
    }

    [Fact]
    public async Task Admin_endpoints_enforce_pending_approved_paid_order()
    {
        var (db, wallet, s) = await ArrangeAsync();
        using var _ = db;
        var w = (await wallet.RequestWithdrawalAsync(s.TechUserId, 100m, "tech@upi")).Data!;
        var admin = new AdminWithdrawalsApiController(wallet).As("admin-1", "Admin");

        Assert.Equal(409, (await admin.MarkPaid(w.WithdrawalId)).Status());
        Assert.Equal(200, (await admin.Approve(w.WithdrawalId)).Status());
        Assert.Equal(409, (await admin.Approve(w.WithdrawalId)).Status());
        Assert.Equal(200, (await admin.MarkPaid(w.WithdrawalId)).Status());
        Assert.Equal(409, (await admin.Reject(w.WithdrawalId, new WithdrawalRejectRequest("late"))).Status());
        Assert.Equal(404, (await admin.Approve(9999)).Status());
    }
}
