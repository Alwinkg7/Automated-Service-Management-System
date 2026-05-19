// =================================================================
//  ServiceRequestService.cs — ServiceApp.Services/Implementations
//
//  Concrete implementation of IServiceRequestService.
//  This is where ALL business logic lives.
//
//  KEY PATTERNS USED:
//
//  1. Result<T> pattern — never throw exceptions for business rules.
//     Return Result.Failure("message") for expected failures.
//     The controller checks IsSuccess and shows the error.
//
//  2. Unit of Work transactions — multi-step operations that must
//     be atomic are wrapped in BeginTransaction/Commit/Rollback.
//
//  3. ServiceHistory logging — every status change gets a row
//     in ServiceHistories so we have a full audit trail.
//     The private LogHistoryAsync method handles this consistently.
//
//  4. Status machine enforcement — ValidateTransition() checks
//     that the requested status change is allowed. The chain is:
//     Pending → Assigned → InProgress → Billed → Completed
//     Any other jump is rejected with a clear error message.
// =================================================================

using Microsoft.Extensions.Logging;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Services.Implementations
{
    public class ServiceRequestService : IServiceRequestService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<ServiceRequestService> _logger;

        public ServiceRequestService(
            IUnitOfWork uow,
            ILogger<ServiceRequestService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // =============================================================
        //  CREATE REQUEST
        //  Customer submits a new service request.
        //  Always starts at Pending — no assignment yet.
        // =============================================================
        public async Task<Result<ServiceRequest>> CreateRequestAsync(
            string customerId,
            string description,
            ServiceCategory category,
            string address,
            string pinCode,
            DateTime preferredDateTime)
        {
            // Basic guard — customer must exist
            var customer = await _uow.Users.GetByIdAsync(customerId);
            if (customer == null)
                return Result<ServiceRequest>.Failure(
                    "Customer account not found.");

            await _uow.BeginTransactionAsync();
            try
            {
                // Create the request
                var request = new ServiceRequest
                {
                    CustomerId = customerId,
                    Description = description,
                    Category = category,
                    Address = address,
                    PinCode = pinCode,
                    PreferredDateTime = preferredDateTime,
                    Status = RequestStatus.Pending,
                    CreatedAt = DateTime.UtcNow
                };

                await _uow.ServiceRequests.AddAsync(request);

                // Save first so we get the RequestId (needed for history)
                await _uow.SaveChangesAsync();

                // Log the first history entry — request created
                await LogHistoryAsync(
                    request.RequestId,
                    RequestStatus.Pending,
                    customerId,
                    "Service request created by customer.");

                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "Request #{RequestId} created by customer {CustomerId} " +
                    "for category {Category}",
                    request.RequestId, customerId, category);

                return Result<ServiceRequest>.Success(request);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Failed to create request for customer {CustomerId}",
                    customerId);
                return Result<ServiceRequest>.Failure(
                    "Failed to create request. Please try again.");
            }
        }

        // =============================================================
        //  ASSIGN TECHNICIAN (Admin action)
        //
        //  Validates:
        //    - Request exists and is Pending
        //    - Technician exists and is Available
        //    - Technician skill matches request category
        //
        //  Side effects (all in one transaction):
        //    - Request.Status → Assigned
        //    - Request.AssignedTechnicianProfileId set
        //    - Request.UpdatedAt stamped
        //    - ServiceHistory row inserted
        // =============================================================
        public async Task<Result<ServiceRequest>> AssignTechnicianAsync(
            int requestId,
            int technicianProfileId,
            string adminUserId)
        {
            // Load the request
            var request = await _uow.ServiceRequests
                .GetByIdAsync(requestId);
            if (request == null)
                return Result<ServiceRequest>.Failure(
                    "Service request not found.");

            // Must be Pending to assign
            if (request.Status != RequestStatus.Pending)
                return Result<ServiceRequest>.Failure(
                    $"Cannot assign — request is currently {request.Status}. " +
                    "Only Pending requests can be assigned.");

            // Load the technician
            var tech = await _uow.TechnicianProfiles
                .GetWithUserAsync(technicianProfileId);
            if (tech == null)
                return Result<ServiceRequest>.Failure(
                    "Technician not found.");

            // Must be Available
            if (tech.Status != TechnicianStatus.Available)
                return Result<ServiceRequest>.Failure(
                    $"{tech.User.FullName} is currently {tech.Status} " +
                    "and cannot be assigned.");

            // Skill must match category
            if (tech.Skill != request.Category)
                return Result<ServiceRequest>.Failure(
                    $"{tech.User.FullName} is a {tech.Skill} " +
                    $"but this request needs a {request.Category}.");

            await _uow.BeginTransactionAsync();
            try
            {
                // Update request
                request.AssignedTechnicianProfileId = technicianProfileId;
                request.Status = RequestStatus.Assigned;
                request.UpdatedAt = DateTime.UtcNow;
                _uow.ServiceRequests.Update(request);

                // Log history
                await LogHistoryAsync(
                    requestId,
                    RequestStatus.Assigned,
                    adminUserId,
                    $"Technician {tech.User.FullName} assigned by admin.");

                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "Request #{RequestId} assigned to technician " +
                    "{TechnicianId} by admin {AdminId}",
                    requestId, technicianProfileId, adminUserId);

                return Result<ServiceRequest>.Success(request);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Failed to assign technician to request #{RequestId}",
                    requestId);
                return Result<ServiceRequest>.Failure(
                    "Assignment failed. Please try again.");
            }
        }

        // =============================================================
        //  ACCEPT JOB (Technician action)
        //
        //  Validates:
        //    - Request is Assigned
        //    - The accepting technician is the one assigned
        //
        //  Side effects (atomic transaction):
        //    - Request.Status → InProgress
        //    - TechnicianProfile.Status → Busy
        //    - ServiceHistory row inserted
        //
        //  WHY TRANSACTION?
        //  If we mark the request InProgress but fail to mark
        //  the technician Busy, they could get assigned another job
        //  while still on this one. Both must succeed or both fail.
        // =============================================================
        public async Task<Result<ServiceRequest>> AcceptJobAsync(
            int requestId,
            string technicianUserId)
        {
            // Load request with full details
            var request = await _uow.ServiceRequests
                .GetWithDetailsAsync(requestId);
            if (request == null)
                return Result<ServiceRequest>.Failure(
                    "Request not found.");

            // Must be Assigned
            if (request.Status != RequestStatus.Assigned)
                return Result<ServiceRequest>.Failure(
                    $"Cannot accept — request is {request.Status}, not Assigned.");

            // Load the technician profile for this user
            var tech = await _uow.TechnicianProfiles
                .GetByUserIdAsync(technicianUserId);
            if (tech == null)
                return Result<ServiceRequest>.Failure(
                    "Technician profile not found.");

            // Verify this technician is the assigned one
            if (request.AssignedTechnicianProfileId != tech.TechnicianProfileId)
                return Result<ServiceRequest>.Failure(
                    "You are not assigned to this request.");

            await _uow.BeginTransactionAsync();
            try
            {
                // Update request status
                request.Status = RequestStatus.InProgress;
                request.UpdatedAt = DateTime.UtcNow;
                _uow.ServiceRequests.Update(request);

                // Mark technician as Busy — blocks new assignments
                tech.Status = TechnicianStatus.Busy;
                _uow.TechnicianProfiles.Update(tech);

                // Log history
                await LogHistoryAsync(
                    requestId,
                    RequestStatus.InProgress,
                    technicianUserId,
                    "Technician accepted the job. Work in progress.");

                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "Request #{RequestId} accepted by technician {TechId}. " +
                    "Technician marked Busy.",
                    requestId, tech.TechnicianProfileId);

                return Result<ServiceRequest>.Success(request);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Failed to accept job #{RequestId}", requestId);
                return Result<ServiceRequest>.Failure(
                    "Failed to accept job. Please try again.");
            }
        }

        // =============================================================
        //  REJECT JOB (Technician action)
        //
        //  Technician declines an assigned job.
        //  Request goes back to Pending — admin must assign again.
        //  Technician status stays Available.
        // =============================================================
        public async Task<Result> RejectJobAsync(
            int requestId,
            string technicianUserId)
        {
            var request = await _uow.ServiceRequests
                .GetByIdAsync(requestId);
            if (request == null)
                return Result.Failure("Request not found.");

            if (request.Status != RequestStatus.Assigned)
                return Result.Failure(
                    "Can only reject Assigned requests.");

            var tech = await _uow.TechnicianProfiles
                .GetByUserIdAsync(technicianUserId);
            if (tech == null)
                return Result.Failure("Technician profile not found.");

            if (request.AssignedTechnicianProfileId != tech.TechnicianProfileId)
                return Result.Failure(
                    "You are not assigned to this request.");

            await _uow.BeginTransactionAsync();
            try
            {
                // Put request back to Pending for re-assignment
                request.Status = RequestStatus.Pending;
                request.AssignedTechnicianProfileId = null;
                request.UpdatedAt = DateTime.UtcNow;
                _uow.ServiceRequests.Update(request);

                await LogHistoryAsync(
                    requestId,
                    RequestStatus.Pending,
                    technicianUserId,
                    "Job rejected by technician. Request returned to Pending queue.");

                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "Request #{RequestId} rejected by technician {TechId}. " +
                    "Back to Pending.",
                    requestId, tech.TechnicianProfileId);

                return Result.Success();
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Failed to reject job #{RequestId}", requestId);
                return Result.Failure(
                    "Failed to reject job. Please try again.");
            }
        }

        // =============================================================
        //  CANCEL REQUEST
        //  Can be cancelled when Pending or Assigned.
        //  Cannot cancel once work has started (InProgress+).
        // =============================================================
        public async Task<Result> CancelRequestAsync(
            int requestId,
            string cancelledByUserId)
        {
            var request = await _uow.ServiceRequests
                .GetByIdAsync(requestId);
            if (request == null)
                return Result.Failure("Request not found.");

            // Only allow cancel before work starts
            if (request.Status != RequestStatus.Pending &&
                request.Status != RequestStatus.Assigned)
                return Result.Failure(
                    $"Cannot cancel a request that is {request.Status}. " +
                    "Work may have already started.");

            await _uow.BeginTransactionAsync();
            try
            {
                // If a technician was assigned, free them up
                if (request.AssignedTechnicianProfileId.HasValue)
                {
                    var tech = await _uow.TechnicianProfiles
                        .GetByIdAsync(request.AssignedTechnicianProfileId.Value);
                    if (tech != null)
                    {
                        tech.Status = TechnicianStatus.Available;
                        _uow.TechnicianProfiles.Update(tech);
                    }
                }

                request.Status = RequestStatus.Cancelled;
                request.UpdatedAt = DateTime.UtcNow;
                _uow.ServiceRequests.Update(request);

                await LogHistoryAsync(
                    requestId,
                    RequestStatus.Cancelled,
                    cancelledByUserId,
                    "Request cancelled.");

                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "Request #{RequestId} cancelled by {UserId}",
                    requestId, cancelledByUserId);

                return Result.Success();
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Failed to cancel request #{RequestId}", requestId);
                return Result.Failure(
                    "Failed to cancel. Please try again.");
            }
        }

        // =============================================================
        //  SUBMIT RATING (Customer action after Completed)
        //  Updates technician's average rating.
        // =============================================================
        public async Task<Result> SubmitRatingAsync(
            int requestId,
            string customerId,
            int rating,
            string? feedback)
        {
            if (rating < 1 || rating > 5)
                return Result.Failure("Rating must be between 1 and 5.");

            var request = await _uow.ServiceRequests
                .GetWithDetailsAsync(requestId);
            if (request == null)
                return Result.Failure("Request not found.");

            if (request.CustomerId != customerId)
                return Result.Failure(
                    "You can only rate your own requests.");

            if (request.Status != RequestStatus.Completed)
                return Result.Failure(
                    "You can only rate completed requests.");

            if (request.CustomerRating.HasValue)
                return Result.Failure("You have already rated this request.");

            await _uow.BeginTransactionAsync();
            try
            {
                // Save rating on the request
                request.CustomerRating = rating;
                request.CustomerFeedback = feedback;
                _uow.ServiceRequests.Update(request);

                // Recalculate technician's average rating
                if (request.AssignedTechnicianProfileId.HasValue)
                {
                    var tech = await _uow.TechnicianProfiles
                        .GetByIdAsync(
                            request.AssignedTechnicianProfileId.Value);
                    if (tech != null)
                    {
                        // Weighted average using total jobs count
                        var totalJobs = tech.TotalJobsCompleted;
                        if (totalJobs > 0)
                        {
                            tech.Rating = Math.Round(
                                ((tech.Rating * totalJobs) + rating)
                                / (totalJobs + 1), 2);
                        }
                        else
                        {
                            tech.Rating = rating;
                        }
                        _uow.TechnicianProfiles.Update(tech);
                    }
                }

                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "Request #{RequestId} rated {Rating} stars by customer",
                    requestId, rating);

                return Result.Success();
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Failed to submit rating for request #{RequestId}",
                    requestId);
                return Result.Failure(
                    "Failed to submit rating. Please try again.");
            }
        }

        // =============================================================
        //  QUERIES
        // =============================================================
        public async Task<Result<ServiceRequest>> GetRequestDetailsAsync(
            int requestId)
        {
            var request = await _uow.ServiceRequests
                .GetWithDetailsAsync(requestId);
            if (request == null)
                return Result<ServiceRequest>.Failure(
                    "Request not found.");
            return Result<ServiceRequest>.Success(request);
        }

        public async Task<Result<IEnumerable<ServiceRequest>>>
            GetCustomerRequestsAsync(string customerId)
        {
            var requests = await _uow.ServiceRequests
                .GetByCustomerIdAsync(customerId);
            return Result<IEnumerable<ServiceRequest>>.Success(requests);
        }

        public async Task<Result<IEnumerable<ServiceRequest>>>
        GetAllRequestsAsync(RequestStatus? filterStatus = null)
        {
            IEnumerable<ServiceRequest> requests;

            if (filterStatus.HasValue)
                // Filtered — existing method already has Include() calls
                requests = await _uow.ServiceRequests
                    .GetByStatusAsync(filterStatus.Value);
            else
                // All requests — use the new method that loads navigation properties
                requests = await _uow.ServiceRequests
                    .GetAllWithDetailsAsync();

            return Result<IEnumerable<ServiceRequest>>.Success(requests);
        }

        public async Task<Result<IEnumerable<ServiceRequest>>>
            GetTechnicianJobsAsync(int technicianProfileId)
        {
            var jobs = await _uow.ServiceRequests
                .GetByTechnicianIdAsync(technicianProfileId);
            return Result<IEnumerable<ServiceRequest>>.Success(jobs);
        }

        public async Task<Result<IEnumerable<TechnicianProfile>>>
            GetAvailableTechniciansAsync(ServiceCategory category)
        {
            var techs = await _uow.TechnicianProfiles
                .GetAvailableBySkillAsync(category);

            if (!techs.Any())
                return Result<IEnumerable<TechnicianProfile>>.Failure(
                    $"No available technicians for {category} right now.");

            return Result<IEnumerable<TechnicianProfile>>.Success(techs);
        }

        // =============================================================
        //  PRIVATE HELPER — log a ServiceHistory row
        //
        //  Called after every status change. Gives us a full audit
        //  trail of who changed what and when for every request.
        //  The timeline on the Details page is built from these rows.
        // =============================================================
        private async Task LogHistoryAsync(
            int requestId,
            RequestStatus status,
            string changedByUserId,
            string note)
        {
            var history = new ServiceHistory
            {
                RequestId = requestId,
                Status = status,
                ChangedByUserId = changedByUserId,
                Note = note,
                ChangedAt = DateTime.UtcNow
            };

            await _uow.ServiceHistories.AddAsync(history);
            // Note: SaveChangesAsync is NOT called here.
            // The calling method's transaction will save everything.
        }
    }
}