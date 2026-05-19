// =================================================================
//  UserListViewModel.cs
//
//  Powers the admin "All Users" management page.
//  Shows all users grouped by role with quick actions.
// =================================================================

using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Web.Areas.Admin.Models
{
    public class UserListViewModel
    {
        public List<ApplicationUser> AllUsers { get; set; }
            = new List<ApplicationUser>();

        // Currently active filter
        public UserRole? CurrentFilter { get; set; }

        // Counts for the filter tabs
        public int AdminCount { get; set; }
        public int TechnicianCount { get; set; }
        public int CustomerCount { get; set; }
        public int TotalCount { get; set; }
    }
}