using ServiceApp.Core.Common;
using ServiceApp.Core.Enums;
using ServiceApp.Services.Implementations;

namespace ServiceApp.Tests;

/// <summary>BUG-22 (server-side balance) and BUG-18 (withdrawal state machine).</summary>
public class WalletRegressionTests
{
    private static async Task<(TestDb Db, WalletService Wallet, Scenario S)> ArrangePaidJobAsync(decimal amount)
    {
        var db = new TestDb();
        var s = await db.SeedBilledJobAsync(amount);
        var request = (await db.Uow.ServiceRequests.GetByIdAsync(s.RequestId))!;
        request.Status = RequestStatus.Completed;
        var bill = (await db.Uow.Bills.GetByIdAsync(s.BillId))!;
        bill.PaymentStatus = PaymentStatus.Paid;
        bill.PaidAt = DateTime.UtcNow;
        await db.Context.SaveChangesAsync();
        return (db, new WalletService(db.Uow, TestDb.Log<WalletService>()), s);
    }

    [Fact]
    public async Task Bug22_Balance_is_net_of_commission_and_over_balance_withdrawal_is_rejected()
    {
        var (db, wallet, s) = await ArrangePaidJobAsync(1000m);
        using var _ = db;

        Assert.Equal(880m, await wallet.GetAvailableBalanceAsync(s.TechUserId));

        // Whatever the client claims is available, only the server balance counts.
        Assert.False((await wallet.RequestWithdrawalAsync(s.TechUserId, 5000m, "tech@upi")).IsSuccess);
        Assert.True((await wallet.RequestWithdrawalAsync(s.TechUserId, 800m, "tech@upi")).IsSuccess);

        // Pending withdrawals reduce the balance, so a second request cannot double-spend.
        Assert.Equal(80m, await wallet.GetAvailableBalanceAsync(s.TechUserId));
        Assert.False((await wallet.RequestWithdrawalAsync(s.TechUserId, 100m, "tech@upi")).IsSuccess);
    }

    [Fact]
    public async Task Bug18_Withdrawal_follows_state_machine()
    {
        var (db, wallet, s) = await ArrangePaidJobAsync(1000m);
        using var _ = db;
        var w = (await wallet.RequestWithdrawalAsync(s.TechUserId, 100m, "tech@upi")).Data!;

        Assert.Equal(ErrorType.Conflict, (await wallet.MarkPaidAsync(w.WithdrawalId, "admin")).ErrorType);
        Assert.True((await wallet.ApproveAsync(w.WithdrawalId, "admin")).IsSuccess);
        Assert.Contains(await wallet.GetOpenWithdrawalsAsync(), x => x.WithdrawalId == w.WithdrawalId);
        Assert.True((await wallet.MarkPaidAsync(w.WithdrawalId, "admin")).IsSuccess);
        Assert.Equal(ErrorType.Conflict, (await wallet.RejectAsync(w.WithdrawalId, "admin", "late")).ErrorType);
        Assert.Equal(ErrorType.Conflict, (await wallet.ApproveAsync(w.WithdrawalId, "admin")).ErrorType);
        Assert.DoesNotContain(await wallet.GetOpenWithdrawalsAsync(), x => x.WithdrawalId == w.WithdrawalId);
    }

    [Theory]
    [InlineData(WithdrawalStatus.Pending, WithdrawalStatus.Approved, true)]
    [InlineData(WithdrawalStatus.Approved, WithdrawalStatus.Paid, true)]
    [InlineData(WithdrawalStatus.Pending, WithdrawalStatus.Rejected, true)]
    [InlineData(WithdrawalStatus.Pending, WithdrawalStatus.Paid, false)]
    [InlineData(WithdrawalStatus.Paid, WithdrawalStatus.Approved, false)]
    [InlineData(WithdrawalStatus.Rejected, WithdrawalStatus.Approved, false)]
    public void Bug18_Transition_table(string from, string to, bool allowed) =>
        Assert.Equal(allowed, WithdrawalStatus.CanTransition(from, to));
}
