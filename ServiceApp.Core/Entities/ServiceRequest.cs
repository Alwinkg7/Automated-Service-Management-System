// =================================================================
//  ServiceRequest.cs — ServiceApp.Core/Entities
//
//  The CORE entity of the entire system. Think of this as
//  the "ride" in Rapido — everything else revolves around it.
//
//  FULL LIFECYCLE:
//  Customer submits → Pending
//    Admin assigns technician → Assigned
//      Technician accepts → InProgress
//        Technician creates bill → Billed
//          Customer pays → Completed
//
//  Every status change also creates a ServiceHistory row
//  so we have a full audit trail.
// =================================================================

using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Entities
{
    public class ServiceRequest
    {
        public int RequestId { get; set; }

        // ── Who created this request ───────────────────────────────

        // FK to ApplicationUser (the customer)
        // String because Identity Id is a GUID string
        public string CustomerId { get; set; } = string.Empty;
        public virtual ApplicationUser Customer { get; set; } = null!;

        // ── What is needed ─────────────────────────────────────────

        // Free text — "My kitchen tap is leaking badly"
        public string Description { get; set; } = string.Empty;

        // Type of service — matched against TechnicianProfile.Skill
        // during assignment
        public ServiceCategory Category { get; set; }

        // ── Where and when ────────────────────────────────────────

        public string? Address { get; set; }
        public string? PinCode { get; set; }

        // When the customer wants the technician to arrive
        public DateTime PreferredDateTime { get; set; }

        // ── Current state ─────────────────────────────────────────

        // The current lifecycle position — see RequestStatus enum
        public RequestStatus Status { get; set; } = RequestStatus.Pending;

        // ── Assignment ────────────────────────────────────────────

        // Null until admin assigns. FK to TechnicianProfile.
        public int? AssignedTechnicianProfileId { get; set; }
        public virtual TechnicianProfile? AssignedTechnician { get; set; }

        // ── Customer feedback (filled after Completed) ────────────

        public int? CustomerRating { get; set; }        // 1–5 stars
        public string? CustomerFeedback { get; set; }   // written review

        // ── Audit ─────────────────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }        // updated on every status change

        // ── Navigation ────────────────────────────────────────────

        // Full timeline of all status changes for this request
        public virtual ICollection<ServiceHistory> History { get; set; }
            = new List<ServiceHistory>();

        // The bill created by the technician after work is done
        // Null until technician creates it (Status = Billed)
        public virtual Bill? Bill { get; set; }
    }
}