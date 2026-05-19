// =================================================================
//  CustomerProfileViewModel.cs
//
//  Data shown and collected on the Customer profile page.
//  Combines read-only display data (name, email from User)
//  with editable profile fields (address, city, pin code).
//
//  WHY COMBINE BOTH?
//  The profile page shows "Hello John" (from User table)
//  alongside the editable fields (from CustomerProfiles table).
//  One ViewModel carries everything the view needs.
// =================================================================

using System.ComponentModel.DataAnnotations;
using ServiceApp.Core.Enums;

namespace ServiceApp.Web.Areas.Customer.Models
{
    public class CustomerProfileViewModel
    {
        // ── Read-only display fields (from ApplicationUser) ────────
        // Not editable here — email/name changes need
        // a separate "Account Settings" page (Phase 3+)
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        // ── Editable profile fields (saved to CustomerProfiles) ────

        [Required(ErrorMessage = "Address is required")]
        [StringLength(300, ErrorMessage = "Address too long")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required")]
        [StringLength(100, ErrorMessage = "City name too long")]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pin code is required")]
        [StringLength(10, MinimumLength = 6,
            ErrorMessage = "Enter a valid pin code")]
        [Display(Name = "Pin code")]
        public string PinCode { get; set; } = string.Empty;

        // Optional — customer's most-used service type
        // Pre-fills the category when creating a new request
        [Display(Name = "Preferred service")]
        public ServiceCategory? PreferredCategory { get; set; }

        // Profile photo URL — upload handled in Phase 3+
        // For now we just store a URL if provided manually
        [Display(Name = "Profile photo URL")]
        [StringLength(500)]
        public string? AvatarUrl { get; set; }

        // Tells the view whether this is a first-time setup
        // or an edit of existing profile data
        public bool IsExistingProfile { get; set; }
    }
}