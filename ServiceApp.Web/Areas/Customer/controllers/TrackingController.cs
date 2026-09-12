// =================================================================
//  TrackingController.cs
//
//  GET  /Customer/Tracking/Map/{requestId}
//       → Live tracking map page for the customer.
//       → Only accessible when request is InProgress.
//
//  POST /Customer/Tracking/UpdateLocation
//       → Called by the TECHNICIAN'S browser via JS every 30s.
//       → Validates ownership then pushes to customer via SignalR.
//       → Technician calls this endpoint (authorized as Technician)
//
//  We put UpdateLocation here in Customer area but authorize
//  it separately — it's called cross-area by the technician's JS.
// =================================================================

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Web.Areas.Customer.Controllers
{
    [Area("Customer")]
    [Authorize]  // Both Customer and Technician can hit this area
    public class TrackingController : Controller
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notifications;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<TrackingController> _logger;

        public TrackingController(
            IUnitOfWork uow,
            INotificationService notifications,
            UserManager<ApplicationUser> userManager,
            ILogger<TrackingController> logger)
        {
            _uow = uow;
            _notifications = notifications;
            _userManager = userManager;
            _logger = logger;
        }

        // =============================================================
        //  GET /Customer/Tracking/Map/{requestId}
        //  Show the live map page.
        //  Only accessible to the customer who owns the request.
        // =============================================================
        [HttpGet]
        [Authorize(Roles = "Customer")]
        public async Task<IActionResult> Map(int requestId)
        {
            var userId = _userManager.GetUserId(User)!;

            var request = await _uow.ServiceRequests
                .GetWithDetailsAsync(requestId);

            if (request == null)
            {
                TempData["Error"] = "Request not found.";
                return RedirectToAction("Index", "Requests");
            }

            // Security: only the request owner
            if (request.CustomerId != userId)
            {
                TempData["Error"] =
                    "You can only track your own requests.";
                return RedirectToAction("Index", "Requests");
            }

            // Only trackable when InProgress
            if (request.Status != RequestStatus.InProgress)
            {
                TempData["Error"] =
                    $"Tracking is only available for InProgress requests. " +
                    $"This request is {request.Status}.";
                return RedirectToAction("Details", "Requests",
                    new { id = requestId });
            }

            return View(request);
        }

        // =============================================================
        //  POST /Customer/Tracking/UpdateLocation
        //  Called by TECHNICIAN'S JS every 30 seconds.
        //  Receives GPS coordinates, pushes to customer via SignalR.
        //
        //  WHY IN CUSTOMER AREA?
        //  The tracking map page is customer-facing. The endpoint
        //  that feeds it is called by the technician. We put it here
        //  for URL clarity (/Customer/Tracking/UpdateLocation) but
        //  authorize it for Technician role.
        // =============================================================
        [HttpPost]
        [Authorize(Roles = "Technician")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLocation(
            int requestId,
            double lat,
            double lng)
        {
            var userId = _userManager.GetUserId(User)!;

            // Verify the technician owns this job
            var tech = await _uow.TechnicianProfiles
                .GetByUserIdAsync(userId);

            if (tech == null)
                return Json(new
                {
                    success = false,
                    error = "Profile not found."
                });

            var request = await _uow.ServiceRequests
                .GetByIdAsync(requestId);

            if (request == null)
                return Json(new
                {
                    success = false,
                    error = "Request not found."
                });

            if (request.AssignedTechnicianProfileId
                != tech.TechnicianProfileId)
                return Json(new
                {
                    success = false,
                    error = "Not your job."
                });

            if (request.Status != RequestStatus.InProgress)
                return Json(new
                {
                    success = false,
                    error = "Job is not in progress."
                });

            // Push location to customer via SignalR
            await _notifications.PushLocationUpdateAsync(
                request.CustomerId,
                requestId,
                lat,
                lng);

            _logger.LogDebug(
                "Location update for request #{RequestId}: " +
                "{Lat},{Lng}",
                requestId, lat, lng);

            return Json(new { success = true });
        }
    }
}