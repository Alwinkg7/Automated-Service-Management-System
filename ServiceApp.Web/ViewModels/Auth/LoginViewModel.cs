// =================================================================
//  LoginViewModel.cs
//  Shared by all three roles — one login page for everyone.
// =================================================================

using System.ComponentModel.DataAnnotations;

namespace ServiceApp.Web.ViewModels.Auth
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress(ErrorMessage = "Enter a valid email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Password is required")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        // If checked → cookie persists 7 days (set in Program.cs)
        // If unchecked → cookie expires when browser closes
        [Display(Name = "Remember me")]
        public bool RememberMe { get; set; }
    }
}