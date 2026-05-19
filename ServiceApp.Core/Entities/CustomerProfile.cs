// =================================================================
//  CustomerProfile.cs — ServiceApp.Core/Entities
//
//  Stores extra data about a Customer — filled on the
//  "Complete your profile" page after first login.
//
//  WHY SEPARATE FROM ApplicationUser?
//  Clean separation of concerns:
//    - ApplicationUser  = authentication data (email, password)
//    - CustomerProfile  = service-relevant data (address, preferences)
//
//  A customer can exist without a profile (just signed up).
//  The app should handle this gracefully — show a banner:
//  "Complete your profile to book faster."
// =================================================================

using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Entities
{
    public class CustomerProfile
    {
        public int CustomerProfileId { get; set; }

        // Foreign key to ApplicationUser
        // One-to-one: one user → at most one customer profile
        public string UserId { get; set; } = string.Empty;
        public virtual ApplicationUser User { get; set; } = null!;

        // ── Profile fields (filled after first login) ─────────────

        // Default address for service requests
        // Customer can override this per-request
        public string? Address { get; set; }

        // City helps with technician matching (geo-filter in future)
        public string? City { get; set; }

        // Pin code — used later for location-based technician matching
        public string? PinCode { get; set; }

        // Optional profile photo
        public string? AvatarUrl { get; set; }

        // Which type of service they usually book — used for quick re-booking
        public ServiceCategory? PreferredCategory { get; set; } 

        public DateTime? ProfileCompletedAt { get; set; }
    }
}