// =================================================================
//  ApplicationUser.cs — ServiceApp.Core/Entities
//
//  The ONLY login entity. All three roles (Customer, Technician,
//  Admin) have exactly one row here. This table handles:
//    - Password hashing (via Identity)
//    - Login sessions (cookies)
//    - Account lockout
//    - Role assignment
//
//  It does NOT store any role-specific profile data.
//  Profile data lives in 3 separate tables linked by UserId FK.
//
//  IdentityUser already gives us for free:
//    Id (GUID string), UserName, Email, PasswordHash,
//    PhoneNumber, LockoutEnabled, AccessFailedCount, etc.
// =================================================================

using Microsoft.AspNetCore.Identity;
using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Entities
{
    public class ApplicationUser : IdentityUser
    {
        // ── Basic info collected at signup ────────────────────────

        // Full display name — shown in dashboards and notifications
        public string FullName { get; set; } = string.Empty;

        // We keep our own Phone field for clean access in views.
        // (IdentityUser.PhoneNumber exists but requires confirmation flow)
        public string Phone { get; set; } = string.Empty;

        // Which role this user has — Customer, Technician, or Admin.
        // Stored as string in DB: "Customer", "Technician", "Admin"
        // Also stored in AspNetUserRoles (Identity table) for [Authorize] to work.
        // We keep it here too for fast single-table lookups.
        public UserRole Role { get; set; }

        // ── Audit fields ──────────────────────────────────────────

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Soft delete — we never hard delete users.
        // Set to false to deactivate an account without losing history.
        public bool IsActive { get; set; } = true;

        // ── Navigation properties (EF loads these via Include()) ──

        // Exactly ONE of these will be non-null depending on Role.
        // If Role = Customer   → CustomerProfile is populated
        // If Role = Technician → TechnicianProfile is populated
        // If Role = Admin      → AdminProfile is populated
        public virtual CustomerProfile? CustomerProfile { get; set; }
        public virtual TechnicianProfile? TechnicianProfile { get; set; }
        public virtual AdminProfile? AdminProfile { get; set; }

        // A customer's service requests — linked via ServiceRequest.CustomerId
        public virtual ICollection<ServiceRequest> ServiceRequests { get; set; }
            = new List<ServiceRequest>();
    }
}