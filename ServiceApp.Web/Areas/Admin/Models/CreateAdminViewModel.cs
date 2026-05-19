// =================================================================
//  CreateAdminViewModel.cs
//
//  Data collected when an existing admin creates a new admin.
//  Only basic info needed — profile details filled later on
//  the Admin Profile page after first login.
//
//  SECURITY:
//  This page is [Authorize(Roles = "Admin")] — only existing
//  admins can create new admins. No public access.
// =================================================================

using System.ComponentModel.DataAnnotations;

namespace ServiceApp.Web.Areas.Admin.Models
{
    public class CreateAdminViewModel
    {
        [Required(ErrorMessage = "Full name is required")]
        [StringLength(100, MinimumLength = 2)]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Phone is required")]
        [StringLength(15, MinimumLength = 10)]
        [Display(Name = "Phone number")]
        public string Phone { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, MinimumLength = 8,
            ErrorMessage = "Password must be at least 8 characters")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please confirm the password")]
        [Compare("Password", ErrorMessage = "Passwords do not match")]
        [DataType(DataType.Password)]
        [Display(Name = "Confirm password")]
        public string ConfirmPassword { get; set; } = string.Empty;

        // Optional — can be filled now or later via profile page
        [StringLength(100)]
        public string? Department { get; set; }

        [StringLength(100)]
        public string? Designation { get; set; }
    }
}