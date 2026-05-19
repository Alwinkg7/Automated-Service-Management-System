// =================================================================
//  CreateRequestViewModel.cs
//
//  Data collected from the "Book a Service" form.
//
//  Address is pre-filled from CustomerProfile if it exists —
//  customer can override it per-request (they may want service
//  at a different address than their saved one).
//
//  PreferredDateTime validation:
//  Must be at least 2 hours in the future — we need time to
//  assign a technician before the requested time slot.
// =================================================================

using System.ComponentModel.DataAnnotations;
using ServiceApp.Core.Enums;

namespace ServiceApp.Web.Areas.Customer.Models
{
    public class CreateRequestViewModel
    {
        // What type of service is needed
        // Drives technician matching — only techs with matching Skill
        // will appear in the assignment pool
        [Required(ErrorMessage = "Please select a service category")]
        [Display(Name = "Service type")]
        public ServiceCategory Category { get; set; }

        // What exactly is the problem — the more detail, the better
        [Required(ErrorMessage = "Please describe what you need")]
        [StringLength(1000, MinimumLength = 20,
            ErrorMessage = "Description must be at least 20 characters")]
        [Display(Name = "Describe the problem")]
        public string Description { get; set; } = string.Empty;

        // Where the work needs to happen
        [Required(ErrorMessage = "Address is required")]
        [StringLength(300, ErrorMessage = "Address too long")]
        [Display(Name = "Service address")]
        public string Address { get; set; } = string.Empty;

        [Required(ErrorMessage = "Pin code is required")]
        [StringLength(10, MinimumLength = 6,
            ErrorMessage = "Enter a valid pin code")]
        [Display(Name = "Pin code")]
        public string PinCode { get; set; } = string.Empty;

        // When the customer wants the technician to arrive
        [Required(ErrorMessage = "Please select a preferred date and time")]
        [Display(Name = "Preferred date & time")]
        public DateTime PreferredDateTime { get; set; }
            = DateTime.Now.AddHours(3); // default: 3 hours from now

        // Pre-filled from profile — shown as hint text in the form
        // Not posted back — just used to auto-fill address on GET
        public string? SavedAddress { get; set; }
        public string? SavedPinCode { get; set; }
    }
}