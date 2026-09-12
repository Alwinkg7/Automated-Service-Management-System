// =================================================================
//  EarningsViewModel.cs
//
//  Powers the technician's earnings wallet page.
//  All data computed from existing Bills + Payments tables —
//  no new database tables needed for this feature.
//
//  EARNINGS MODEL:
//  Gross = sum of all paid bill totals for this technician
//  Commission = Gross × commission rate (12% standard)
//  Net = Gross - Commission (what tech actually receives)
// =================================================================

using ServiceApp.Core.Entities;

namespace ServiceApp.Web.Areas.Technician.Models
{
    public class EarningsViewModel
    {
        // ── Summary cards ──────────────────────────────────────────
        public decimal GrossEarningsAllTime { get; set; }
        public decimal CommissionAllTime { get; set; }
        public decimal NetEarningsAllTime { get; set; }

        public decimal GrossEarningsThisMonth { get; set; }
        public decimal NetEarningsThisMonth { get; set; }

        public decimal GrossEarningsThisWeek { get; set; }
        public decimal NetEarningsThisWeek { get; set; }

        public int TotalJobsCompleted { get; set; }
        public decimal AveragePerJob { get; set; }

        // Commission rate — 12% standard, 8% if Pro
        public decimal CommissionRate { get; set; } = 0.12m;

        // ── Charts ─────────────────────────────────────────────────
        // Monthly earnings for last 6 months
        public List<string> MonthLabels { get; set; } = new();
        public List<decimal> GrossValues { get; set; } = new();
        public List<decimal> NetValues { get; set; } = new();

        // Daily earnings for last 30 days
        public List<string> DayLabels { get; set; } = new();
        public List<decimal> DayValues { get; set; } = new();

        // ── Payout history ─────────────────────────────────────────
        // Each completed job = one payout entry
        public List<PayoutEntry> PayoutHistory { get; set; } = new();

        // ── Withdrawal requests ────────────────────────────────────
        public List<WithdrawalRequest> WithdrawalRequests { get; set; }
            = new();

        // Pending withdrawal total
        public decimal PendingWithdrawal { get; set; }

        // Available to withdraw = Net - already withdrawn - pending
        public decimal AvailableToWithdraw { get; set; }
    }

    // One completed job = one payout entry
    public class PayoutEntry
    {
        public int RequestId { get; set; }
        public int BillId { get; set; }
        public string CustomerName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal GrossAmount { get; set; }
        public decimal Commission { get; set; }
        public decimal NetAmount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public DateTime PaidAt { get; set; }
    }

    // One withdrawal request submitted by the technician
    public class WithdrawalRequest
    {
        public int Id { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; } = string.Empty;
        public string UpiId { get; set; } = string.Empty;
        public string? AdminNote { get; set; }
        public DateTime RequestedAt { get; set; }
        public DateTime? ProcessedAt { get; set; }
    }

    // For the withdrawal request form
    public class WithdrawViewModel
    {
        public decimal AvailableAmount { get; set; }
        public decimal RequestAmount { get; set; }
        public string UpiId { get; set; } = string.Empty;
    }
}