namespace ServiceApp.Core.Common;

/// <summary>
/// Withdrawal states (stored as strings). Allowed transitions:
/// Pending → Approved → Paid, and Pending/Approved → Rejected. Paid and Rejected are final.
/// </summary>
public static class WithdrawalStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Paid = "Paid";
    public const string Rejected = "Rejected";

    public static bool CanTransition(string from, string to) => (from, to) switch
    {
        (Pending, Approved) => true,
        (Approved, Paid) => true,
        (Pending, Rejected) => true,
        (Approved, Rejected) => true,
        _ => false
    };
}
