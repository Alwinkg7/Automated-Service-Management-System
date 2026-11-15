using Microsoft.Extensions.Logging;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Services.Implementations;

/// <summary>Technician balance and the withdrawal state machine (see <see cref="WithdrawalStatus"/>).</summary>
public class WalletService : IWalletService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<WalletService> _logger;

    public WalletService(IUnitOfWork uow, ILogger<WalletService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public decimal CommissionRate => 0.12m;

    public async Task<decimal> GetAvailableBalanceAsync(string technicianUserId)
    {
        var tech = await _uow.TechnicianProfiles.GetByUserIdAsync(technicianUserId);
        if (tech == null) return 0m;

        var gross = (await _uow.ServiceRequests.GetByTechnicianIdAsync(tech.TechnicianProfileId))
            .Where(j => j.Status == RequestStatus.Completed && j.Bill is { PaymentStatus: PaymentStatus.Paid })
            .Sum(j => j.Bill!.TotalAmount);
        var net = Math.Round(gross * (1 - CommissionRate), 2);

        var committed = (await _uow.Withdrawals.GetByTechnicianAsync(technicianUserId))
            .Where(w => w.Status is WithdrawalStatus.Pending or WithdrawalStatus.Approved or WithdrawalStatus.Paid)
            .Sum(w => w.Amount);

        return Math.Max(0m, net - committed);
    }

    public async Task<Result<TechnicianWithdrawal>> RequestWithdrawalAsync(string technicianUserId, decimal amount, string upiId)
    {
        amount = Math.Round(amount, 2);
        if (amount <= 0)
            return Result<TechnicianWithdrawal>.Failure("Enter an amount greater than zero.");
        if (string.IsNullOrWhiteSpace(upiId) || upiId.Trim().Length > 100)
            return Result<TechnicianWithdrawal>.Failure("A valid UPI ID is required.");

        var available = await GetAvailableBalanceAsync(technicianUserId);
        if (amount > available)
            return Result<TechnicianWithdrawal>.Failure($"Maximum available: ₹{available:N2}", ErrorType.BusinessRule);

        var withdrawal = new TechnicianWithdrawal
        {
            TechnicianUserId = technicianUserId,
            Amount = amount,
            Status = WithdrawalStatus.Pending,
            UpiId = upiId.Trim(),
            RequestedAt = DateTime.UtcNow
        };
        await _uow.Withdrawals.AddAsync(withdrawal);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Withdrawal request {Amount} by technician {UserId}", amount, technicianUserId);
        return Result<TechnicianWithdrawal>.Success(withdrawal);
    }

    public Task<IEnumerable<TechnicianWithdrawal>> GetOpenWithdrawalsAsync() =>
        _uow.Withdrawals.GetOpenAsync();

    public Task<Result<TechnicianWithdrawal>> ApproveAsync(int withdrawalId, string adminUserId) =>
        TransitionAsync(withdrawalId, WithdrawalStatus.Approved, adminUserId, note: null);

    public Task<Result<TechnicianWithdrawal>> MarkPaidAsync(int withdrawalId, string adminUserId) =>
        TransitionAsync(withdrawalId, WithdrawalStatus.Paid, adminUserId, note: null);

    public Task<Result<TechnicianWithdrawal>> RejectAsync(int withdrawalId, string adminUserId, string? adminNote) =>
        TransitionAsync(withdrawalId, WithdrawalStatus.Rejected, adminUserId, adminNote);

    private async Task<Result<TechnicianWithdrawal>> TransitionAsync(int id, string to, string adminUserId, string? note)
    {
        var w = await _uow.Withdrawals.GetByIdAsync(id);
        if (w == null)
            return Result<TechnicianWithdrawal>.Failure("Withdrawal not found.", ErrorType.NotFound);

        if (!WithdrawalStatus.CanTransition(w.Status, to))
            return Result<TechnicianWithdrawal>.Failure(
                $"Cannot move a {w.Status} withdrawal to {to}.", ErrorType.Conflict);

        w.Status = to;
        if (note != null)
            w.AdminNote = note.Trim().Length > 500 ? note.Trim()[..500] : note.Trim();
        if (to is WithdrawalStatus.Paid or WithdrawalStatus.Rejected)
            w.ProcessedAt = DateTime.UtcNow;

        _uow.Withdrawals.Update(w);
        await _uow.SaveChangesAsync();

        _logger.LogInformation("Withdrawal #{Id} moved to {Status} by admin {AdminId}", id, to, adminUserId);
        return Result<TechnicianWithdrawal>.Success(w);
    }
}
