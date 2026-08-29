// =================================================================
//  AutoAssignmentService.cs — ServiceApp.Services/Implementations
//
//  The background auto-assignment engine.
//
//  CALLED BY: Hangfire recurring job every 2 minutes
//  DEPENDS ON: IUnitOfWork, INotificationService, ILogger
//
//  KEY DESIGN DECISIONS:
//
//  1. FIFO queue — oldest Pending request is processed first.
//     Customers who waited longer get served first.
//
//  2. Skill matching — only Available technicians whose Skill
//     matches the request Category are considered.
//
//  3. Rating priority — best rated technician is assigned first.
//     Customers consistently get high-quality service.
//
//  4. Atomic assignment — request status + history log happen
//     in one transaction. No half-assignments.
//
//  5. Idempotent — if the same request is Pending across two
//     Hangfire cycles, the second cycle skips it (already Assigned
//     from the first cycle, or still no tech available).
//
//  6. Cycle limit — processes max 20 requests per run to avoid
//     long-running jobs blocking the Hangfire thread.
// =================================================================

using Microsoft.Extensions.Logging;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Services.Implementations
{
    public class AutoAssignmentService : IAutoAssignmentService
    {
        private readonly IUnitOfWork _uow;
        private readonly INotificationService _notifications;
        private readonly ILogger<AutoAssignmentService> _logger;

        // Max requests to process per Hangfire cycle
        // Prevents long-running jobs if there is a huge backlog
        private const int MaxPerCycle = 20;

        public AutoAssignmentService(
            IUnitOfWork uow,
            INotificationService notifications,
            ILogger<AutoAssignmentService> logger)
        {
            _uow = uow;
            _notifications = notifications;
            _logger = logger;
        }

        // =============================================================
        //  MAIN ENGINE — runs every 2 minutes via Hangfire
        // =============================================================
        public async Task RunAsync()
        {
            _logger.LogInformation(
                "Auto-assignment cycle started at {Time}", DateTime.UtcNow);

            // Load all pending requests oldest first (FIFO queue)
            var pendingRequests = await _uow.ServiceRequests
                .GetPendingOrderedByDateAsync();

            var requests = pendingRequests
                .Take(MaxPerCycle)
                .ToList();

            if (!requests.Any())
            {
                _logger.LogInformation(
                    "Auto-assignment: no pending requests. Cycle done.");
                return;
            }

            _logger.LogInformation(
                "Auto-assignment: processing {Count} pending requests",
                requests.Count);

            int assigned = 0;
            int skipped = 0;

            foreach (var request in requests)
            {
                var success = await TryAssignRequestAsync(request.RequestId);
                if (success) assigned++;
                else skipped++;
            }

            _logger.LogInformation(
                "Auto-assignment cycle complete. " +
                "Assigned: {Assigned}, Skipped (no tech): {Skipped}",
                assigned, skipped);
        }

        // =============================================================
        //  TRY ASSIGN ONE REQUEST
        //  Returns true if a technician was found and assigned.
        //  Returns false if no available technician matches.
        // =============================================================
        public async Task<bool> TryAssignRequestAsync(int requestId)
        {
            // Reload fresh — status may have changed since the batch load
            var request = await _uow.ServiceRequests
                .GetWithDetailsAsync(requestId);

            if (request == null)
            {
                _logger.LogWarning(
                    "Auto-assign: request #{RequestId} not found", requestId);
                return false;
            }

            // Skip if already assigned (admin may have manually assigned)
            if (request.Status != RequestStatus.Pending)
            {
                _logger.LogDebug(
                    "Auto-assign: skipping #{RequestId} — status is {Status}",
                    requestId, request.Status);
                return false;
            }

            // Find available technicians matching the skill
            var candidates = await _uow.TechnicianProfiles
                .GetAvailableBySkillAsync(request.Category);

            if (!candidates.Any())
            {
                _logger.LogInformation(
                    "Auto-assign: no Available {Category} technicians " +
                    "for request #{RequestId}",
                    request.Category, requestId);
                return false;
            }

            // ── Pick the best technician ───────────────────────────
            // Priority 1: highest rating
            // Priority 2: fewest total jobs (spread the work fairly)
            var bestTech = candidates
                .OrderByDescending(t => t.Rating)
                .ThenBy(t => t.TotalJobsCompleted)
                .First();

            // ── Assign atomically ──────────────────────────────────
            await _uow.BeginTransactionAsync();
            try
            {
                // Update request
                request.AssignedTechnicianProfileId = bestTech.TechnicianProfileId;
                request.Status = RequestStatus.Assigned;
                request.UpdatedAt = DateTime.UtcNow;
                _uow.ServiceRequests.Update(request);

                // Log history — note it was auto-assigned
                var history = new ServiceHistory
                {
                    RequestId = requestId,
                    Status = RequestStatus.Assigned,
                    ChangedByUserId = "SYSTEM",
                    Note = $"Auto-assigned to {bestTech.User.FullName} " +
                           $"(Rating: {bestTech.Rating:F1}, " +
                           $"Skill: {bestTech.Skill}) " +
                           $"by the auto-assignment engine.",
                    ChangedAt = DateTime.UtcNow
                };
                await _uow.ServiceHistories.AddAsync(history);

                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "Auto-assigned request #{RequestId} ({Category}) " +
                    "to technician {TechName} (Rating: {Rating})",
                    requestId,
                    request.Category,
                    bestTech.User.FullName,
                    bestTech.Rating);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Auto-assign failed for request #{RequestId}", requestId);
                return false;
            }

            // ── Notify via SignalR (after transaction commits) ─────
            // Fire-and-forget — notification failure must not
            // affect the assignment result
            try
            {
                await _notifications.NotifyTechnicianAssignedAsync(
                    bestTech.UserId,
                    requestId,
                    request.Category.ToString(),
                    request.Customer?.FullName ?? "Customer",
                    request.Address ?? "");

                await _notifications.NotifyAdminStatusChangedAsync(
                    requestId, "Assigned");

                // Notify the customer their request was picked up
                await _notifications.NotifyCustomerJobAcceptedAsync(
                    request.CustomerId,
                    requestId,
                    bestTech.User.FullName,
                    bestTech.User.Phone);
            }
            catch (Exception ex)
            {
                // Log but don't fail the assignment
                _logger.LogWarning(ex,
                    "SignalR notification failed after auto-assign " +
                    "for request #{RequestId}", requestId);
            }

            return true;
        }
    }
}