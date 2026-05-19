// =================================================================
//  TechnicianProfileViewModel.cs
//
//  Richer than CustomerProfileViewModel because technician
//  profile is customer-facing — customers see Rating, Bio,
//  and Experience before the technician arrives.
//
//  Skill is NOT editable here — it was set at signup and
//  changing it requires admin approval (Phase 3+).
// =================================================================

using System.ComponentModel.DataAnnotations;
using ServiceApp.Core.Enums;

namespace ServiceApp.Web.Areas.Technician.Models
{
    public class TechnicianProfileViewModel
    {
        // ── Read-only display fields ───────────────────────────────
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        // Skill set at signup — shown but not editable here
        public ServiceCategory Skill { get; set; }
        public decimal Rating { get; set; }
        public int TotalJobsCompleted { get; set; }

        // ── Editable profile fields ────────────────────────────────

        [Required(ErrorMessage = "Please write a short bio")]
        [StringLength(500, MinimumLength = 20,
            ErrorMessage = "Bio must be between 20 and 500 characters")]
        [Display(Name = "About you")]
        public string Bio { get; set; } = string.Empty;

        [Range(0, 50, ErrorMessage = "Enter years between 0 and 50")]
        [Display(Name = "Years of experience")]
        public int? YearsOfExperience { get; set; }

        [Required(ErrorMessage = "Service area pin code is required")]
        [StringLength(10, MinimumLength = 6,
            ErrorMessage = "Enter a valid pin code")]
        [Display(Name = "Service area pin code")]
        public string ServiceAreaPinCode { get; set; } = string.Empty;

        [Display(Name = "Profile photo URL")]
        [StringLength(500)]
        public string? AvatarUrl { get; set; }

        public bool IsExistingProfile { get; set; }
    }
}