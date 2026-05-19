// =================================================================
//  JobDetailViewModel.cs
//
//  Powers the technician's job detail page.
//  Combines the request data with the technician's own profile
//  so the view can show relevant info and the right action buttons.
// =================================================================

using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Web.Areas.Technician.Models
{
    public class JobDetailViewModel
    {
        // The full request with customer, history, bill loaded
        public ServiceRequest Request { get; set; } = null!;

        // The logged-in technician's profile
        // Used to verify ownership and show their current status
        public TechnicianProfile TechnicianProfile { get; set; } = null!;

        // Convenience properties for the view
        public bool CanAccept =>
            Request.Status == RequestStatus.Assigned
            && Request.AssignedTechnicianProfileId
               == TechnicianProfile.TechnicianProfileId;

        public bool CanReject =>
            Request.Status == RequestStatus.Assigned
            && Request.AssignedTechnicianProfileId
               == TechnicianProfile.TechnicianProfileId;

        public bool CanCreateBill =>
            Request.Status == RequestStatus.InProgress
            && Request.AssignedTechnicianProfileId
               == TechnicianProfile.TechnicianProfileId;

        public bool IsMyJob =>
            Request.AssignedTechnicianProfileId
            == TechnicianProfile.TechnicianProfileId;
    }
}