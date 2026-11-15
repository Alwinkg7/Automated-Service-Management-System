using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;

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

    /// <summary>Withdrawals awaiting admin action (Pending and Approved).</summary>
    Task<IEnumerable<TechnicianWithdrawal>> GetOpenWithdrawalsAsync();

    Task<Result<TechnicianWithdrawal>> ApproveAsync(int withdrawalId, string adminUserId);
    Task<Result<TechnicianWithdrawal>> MarkPaidAsync(int withdrawalId, string adminUserId);
    Task<Result<TechnicianWithdrawal>> RejectAsync(int withdrawalId, string adminUserId, string? adminNote);
}
