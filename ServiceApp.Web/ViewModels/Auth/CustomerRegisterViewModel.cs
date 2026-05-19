// =================================================================
//  CustomerRegisterViewModel.cs
//
//  Data collected from the Customer signup form.
//  Only the basics — name, email, phone, password.
//  No address/preferences yet — that goes in the profile
//  update page AFTER first login.
// =================================================================

using System.ComponentModel.DataAnnotations;

namespace ServiceApp.Web.ViewModels.Auth
{
    public class CustomerRegisterViewModel
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