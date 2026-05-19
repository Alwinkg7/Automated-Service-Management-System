// =================================================================
//  AssignTechnicianViewModel.cs
//
//  Powers the Assign screen.
//  Shows request details + list of available technicians
//  whose skill matches the request category.
//
//  Admin picks one technician and clicks Assign.
// =================================================================

using ServiceApp.Core.Entities;

namespace ServiceApp.Web.Areas.Admin.Models
{
    public class AssignTechnicianViewModel
    {
        // The request being assigned
        public ServiceRequest Request { get; set; } = null!;

        // Available technicians whose skill matches
        // the request category — shown as selectable cards
        public List<TechnicianProfile> AvailableTechnicians { get; set; }
            = new List<TechnicianProfile>();

        // The technician the admin selected (posted back)
        public int SelectedTechnicianProfileId { get; set; }
    }
}