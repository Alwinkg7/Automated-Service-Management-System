// =================================================================
//  TechnicianProfile.cs — ServiceApp.Core/Entities
//
//  Stores all technician-specific data — filled on the
//  "Complete your profile" page after first login.
//
//  This is the most data-rich profile because:
//  - The auto-assignment engine reads Skill and Status
//  - Customers see Rating and Bio before a technician arrives
//  - Admin manages them based on this data
//
//  Status changes happen automatically during the job lifecycle
//  (not manually by the technician, except Available ↔ Offline).
// =================================================================

using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Entities
{
    public class TechnicianProfile
    {
        public int TechnicianProfileId { get; set; }

        // Foreign key to ApplicationUser
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; } = null!;

        // ── Assignment engine fields ───────────────────────────────

        // What kind of work this technician does.
        // MUST match ServiceRequest.Category for assignment.
        public ServiceCategory Skill { get; set; }

        // Current availability — the auto-assignment engine ONLY
        // picks technicians where Status == Available.
        // Default is Available when profile is first created.
        public TechnicianStatus Status { get; set; } = TechnicianStatus.Available;

        // ── Public profile fields (visible to customers) ───────────

        public string? AvatarUrl { get; set; }

        // Short description — "10 years experience, specialized in
        // residential wiring and solar panel installation"
        public string? Bio { get; set; }

        // Years of experience — shown to customers for trust
        public int? YearsOfExperience { get; set; }

        // Service area pin code — for geo-filtering (Phase 3+)
        public string? ServiceAreaPinCode { get; set; }

        // ── Computed/tracked fields (updated by the system) ───────

        // Average rating from customer reviews (0.00 to 5.00)
        // Recalculated every time a customer submits a rating.
        // Decimal(3,2) in DB: e.g. 4.75
        public decimal Rating { get; set; } = 0;

        // Total completed jobs — shown on profile ("47 jobs completed")
        public int TotalJobsCompleted { get; set; } = 0;

        public DateTime? ProfileCompletedAt { get; set; }

        // ── Navigation ────────────────────────────────────────────

        // All jobs ever assigned to this technician
        public virtual ICollection<ServiceRequest> AssignedRequests { get; set; }
            = new List<ServiceRequest>();

        // All bills this technician has created
        public virtual ICollection<Bill> Bills { get; set; }
            = new List<Bill>();
    }
}