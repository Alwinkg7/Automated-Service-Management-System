// =================================================================
//  WithdrawalRequestEntity.cs — ServiceApp.Core/Entities
//
//  Technician submits a withdrawal request.
//  Admin reviews and marks it Paid or Rejected.
//  We use a different class name to avoid conflict with the ViewModel.
// =================================================================

namespace ServiceApp.Core.Entities
{
    public class TechnicianWithdrawal
    {
        public int WithdrawalId { get; set; }
        public string TechnicianUserId { get; set; } = string.Empty;
        public decimal Amount { get; set; }

        // "Pending", "Approved", "Paid", "Rejected"
        public string Status { get; set; } = "Pending";

        // UPI ID or bank account to pay to
        public string UpiId { get; set; } = string.Empty;

        public string? AdminNote { get; set; }
        public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
        public DateTime? ProcessedAt { get; set; }

        // Navigation
        public ApplicationUser Technician { get; set; } = null!;
    }
}