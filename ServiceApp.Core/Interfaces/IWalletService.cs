using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Models.Reports;

namespace ServiceApp.Core.Interfaces;

/// <summary>Technician earnings balance and withdrawal workflow.</summary>
public interface IWalletService
{
    /// <summary>Platform commission deducted from every paid bill.</summary>
    decimal CommissionRate { get; }

    /// <summary>
    /// Net earnings (paid bills minus commission) minus withdrawals that are
    /// Paid, Approved or Pending. Always computed server-side.
    /// </summary>
    Task<decimal> GetAvailableBalanceAsync(string technicianUserId);

    /// <summary>Creates a Pending withdrawal if the amount is within the server-computed balance.</summary>
    Task<Result<TechnicianWithdrawal>> RequestWithdrawalAsync(string technicianUserId, decimal amount, string upiId);

    /// <summary>Earnings totals, time series and balance for the technician wallet page.</summary>
    Task<WalletSummary> GetSummaryAsync(string technicianUserId);

    /// <summary>Paid jobs (gross, commission, net), newest first.</summary>
    Task<IReadOnlyList<PayoutEntry>> GetPayoutHistoryAsync(string technicianUserId);

    /// <summary>Withdrawals of one technician, newest first.</summary>
    Task<IEnumerable<TechnicianWithdrawal>> GetWithdrawalsForTechnicianAsync(string technicianUserId);

    /// <summary>Withdrawals awaiting admin action (Pending and Approved).</summary>
    Task<IEnumerable<TechnicianWithdrawal>> GetOpenWithdrawalsAsync();

    /// <summary>All withdrawals, optionally filtered by <see cref="WithdrawalStatus"/>, newest first.</summary>
    Task<IEnumerable<TechnicianWithdrawal>> GetWithdrawalsAsync(string? status);

    Task<Result<TechnicianWithdrawal>> ApproveAsync(int withdrawalId, string adminUserId);
    Task<Result<TechnicianWithdrawal>> MarkPaidAsync(int withdrawalId, string adminUserId);
    Task<Result<TechnicianWithdrawal>> RejectAsync(int withdrawalId, string adminUserId, string? adminNote);
}
