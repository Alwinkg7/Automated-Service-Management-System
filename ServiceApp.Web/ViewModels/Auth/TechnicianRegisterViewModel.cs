// =================================================================
//  TechnicianRegisterViewModel.cs
//
//  Data collected from the Technician signup form.
//  Same basics as customer PLUS Skill selection — because
//  the auto-assignment engine needs this from day one.
//  Other profile details (bio, experience etc.) go on
//  the profile update page after first login.
// =================================================================

using System.ComponentModel.DataAnnotations;
using ServiceApp.Core.Enums;

namespace ServiceApp.Web.ViewModels.Auth
{
    public class TechnicianRegisterViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, MinimumLength = 2,
            ErrorMessage = "Name must be 2–100 characters")]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone number is required")]
        [StringLength(15, MinimumLength = 10,
            ErrorMessage = "Enter a valid phone number")]
        [Display(Name = "Phone number")]
        public string Phone { get; set; } = string.Empty;

        // Skill is required at signup — without it the system
        // cannot match this technician to any service requests
        [Required(ErrorMessage = "Please select your skill")]
        [Display(Name = "Your skill / trade")]
        public ServiceCategory Skill { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8,
            ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm your password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}