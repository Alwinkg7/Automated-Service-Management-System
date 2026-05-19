// =================================================================
//  AdminProfile.cs — ServiceApp.Core/Entities
//
//  Stores admin-specific data — filled after first login.
//  Admins are created by the first/existing admin from the
//  admin panel — never via public signup.
//
//  Kept minimal intentionally — admins are internal staff.
// =================================================================

namespace ServiceApp.Core.Entities
{
    public class AdminProfile
    {
        public int AdminProfileId { get; set; }

        // Foreign key to ApplicationUser
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; } = null!;

        // ── Internal staff fields ──────────────────────────────────

        // e.g. "Operations", "Support", "Finance"
        public string? Department { get; set; }

        // e.g. "Operations Manager", "Support Lead"
        public string? Designation { get; set; }

        public string? AvatarUrl { get; set; }

        // Internal employee/staff ID for HR reference
        public string? EmployeeId { get; set; }

        public DateTime? ProfileCompletedAt { get; set; }
    }
}