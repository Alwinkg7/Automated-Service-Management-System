namespace ServiceApp.Core.Models.Reports;

/// <summary>Technician earnings and balance, in rupees.</summary>
public class WalletSummary
{
    public decimal CommissionRate { get; init; }
    public int TotalJobsCompleted { get; init; }
    public decimal GrossEarningsAllTime { get; init; }
    public decimal CommissionAllTime { get; init; }
    public decimal NetEarningsAllTime { get; init; }
    public decimal AveragePerJob { get; init; }
    public decimal GrossEarningsThisMonth { get; init; }
    public decimal NetEarningsThisMonth { get; init; }
    public decimal GrossEarningsThisWeek { get; init; }
    public decimal NetEarningsThisWeek { get; init; }
    public decimal TotalWithdrawn { get; init; }
    public decimal PendingWithdrawal { get; init; }
    public decimal AvailableToWithdraw { get; init; }
    public IReadOnlyList<LabelledAmount> GrossLast6Months { get; init; } = [];
    public IReadOnlyList<LabelledAmount> GrossLast30Days { get; init; } = [];
}

/// <summary>One paid job in the earnings history of a technician.</summary>
public record PayoutEntry(
    int RequestId,
    int BillId,
    string CustomerName,
    string Category,
    decimal GrossAmount,
    decimal Commission,
    decimal NetAmount,
    string PaymentMethod,
    DateTime PaidAt);
