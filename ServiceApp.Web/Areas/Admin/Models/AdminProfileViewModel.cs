using System.ComponentModel.DataAnnotations;

namespace ServiceApp.Web.Areas.Admin.Models
{
    public class AdminProfileViewModel
    {
        // Read-only
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;

        // Editable
        [StringLength(100)]
        public string? Department { get; set; }

        [StringLength(100)]
        public string? Designation { get; set; }

        [StringLength(50)]
        [Display(Name = "Employee ID")]
        public string? EmployeeId { get; set; }

        [StringLength(500)]
        [Display(Name = "Profile photo URL")]
        public string? AvatarUrl { get; set; }

        public bool IsExistingProfile { get; set; }
    }
}