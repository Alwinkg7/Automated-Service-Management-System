// =================================================================
//  Dispute.cs — ServiceApp.Core/Entities
//
//  A dispute is raised when a customer or technician is unhappy
//  with a completed transaction.
//
//  STATUS FLOW:
//  Open → UnderReview → Resolved (Refunded / Rejected / Closed)
//
//  SLA:
//  Disputes should be resolved within 48 hours.
//  SLA breach = RaisedAt + 48hrs < DateTime.UtcNow && Status = Open
//
//  REFUND:
//  If admin issues a refund, RazorpayRefundId stores the
//  Razorpay refund transaction ID for tracking.
// =================================================================

namespace ServiceApp.Core.Entities
{
    public class Dispute
    {
        public int DisputeId { get; set; }
        public int RequestId { get; set; }
        public string RaisedByUserId { get; set; } = string.Empty;

        // "ServiceQuality","LateArrival","WrongCharge",
        // "Unprofessional","Other"
        public string Reason { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        // "Open","UnderReview","Resolved"
        public string Status { get; set; } = "Open";

        // "Refunded","Rejected","ClosedNoAction"
        public string? Resolution { get; set; }

        public string? AdminNote { get; set; }

        // Refund details — populated if admin issues refund
        public decimal? RefundAmount { get; set; }
        public string? RazorpayRefundId { get; set; }
        public bool RefundIssued { get; set; }

        public DateTime RaisedAt { get; set; }
            = DateTime.UtcNow;
        public DateTime? ResolvedAt { get; set; }

        // SLA deadline — 48 hours from raised
        public DateTime SlaDeadline { get; set; }
            = DateTime.UtcNow.AddHours(48);

        // Is this dispute past its SLA deadline?
        public bool IsSlaBreached =>
            Status != "Resolved" &&
            DateTime.UtcNow > SlaDeadline;

        // Hours remaining before SLA breach
        public double HoursUntilSla =>
            (SlaDeadline - DateTime.UtcNow).TotalHours;

        // Navigation
        public ServiceRequest ServiceRequest { get; set; } = null!;
        public ApplicationUser RaisedBy { get; set; } = null!;
    }
}