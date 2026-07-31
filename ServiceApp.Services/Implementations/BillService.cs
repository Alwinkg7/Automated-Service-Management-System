// =================================================================
//  BillService.cs — ServiceApp.Services/Implementations
//
//  Implements IBillService.
//
//  KEY RULES ENFORCED:
//  - Request must be InProgress before a bill can be created
//  - Only the assigned technician can create the bill
//  - Each request can only have ONE bill (enforced by unique index
//    in DB and by AnyAsync check here)
//  - Bill must have at least one item
//  - All items must have a description and positive price
//  - TotalAmount is calculated here — never trusted from the form
//
//  ATOMIC TRANSACTION:
//  Bill creation + status change + history log all happen
//  in ONE transaction. If any step fails, nothing is saved.
// =================================================================

using Microsoft.Extensions.Logging;
using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;

namespace ServiceApp.Services.Implementations
{
    public class BillService : IBillService
    {
        private readonly IUnitOfWork _uow;
        private readonly ILogger<BillService> _logger;

        public BillService(IUnitOfWork uow, ILogger<BillService> logger)
        {
            _uow = uow;
            _logger = logger;
        }

        // =============================================================
        //  CREATE BILL
        // =============================================================
        public async Task<Result<Bill>> CreateBillAsync(
            int requestId,
            string technicianUserId,
            List<BillItemInput> items)
        {
            // ── Validate inputs ────────────────────────────────────

            if (items == null || !items.Any())
                return Result<Bill>.Failure(
                    "Bill must have at least one item.");

            // Filter out any empty rows the form may have sent
            var validItems = items
                .Where(i => !string.IsNullOrWhiteSpace(i.Description)
                         && i.UnitPrice > 0
                         && i.Quantity > 0)
                .ToList();

            if (!validItems.Any())
                return Result<Bill>.Failure(
                    "Please add at least one valid item " +
                    "with a description and price.");

            // ── Load request ───────────────────────────────────────

            var request = await _uow.ServiceRequests
                .GetWithDetailsAsync(requestId);
            if (request == null)
                return Result<Bill>.Failure("Request not found.");

            // Must be InProgress to create a bill
            if (request.Status != RequestStatus.InProgress)
                return Result<Bill>.Failure(
                    $"Cannot create a bill — request is {request.Status}." +
                    " Request must be InProgress.");

            // ── Verify ownership ───────────────────────────────────

            var tech = await _uow.TechnicianProfiles
                .GetByUserIdAsync(technicianUserId);
            if (tech == null)
                return Result<Bill>.Failure(
                    "Technician profile not found.");

            if (request.AssignedTechnicianProfileId
                != tech.TechnicianProfileId)
                return Result<Bill>.Failure(
                    "You can only create bills for jobs assigned to you.");

            // ── Check no existing bill ─────────────────────────────

            var existingBill = await _uow.Bills
                .GetByRequestIdAsync(requestId);
            if (existingBill != null)
                return Result<Bill>.Failure(
                    "A bill already exists for this request. " +
                    "Cannot create a second bill.");

            // ── Build bill + items ─────────────────────────────────

            // Calculate total from items — never trust the form total
            var totalAmount = validItems
                .Sum(i => i.Quantity * i.UnitPrice);

            if (totalAmount <= 0)
                return Result<Bill>.Failure(
                    "Bill total must be greater than zero.");

            await _uow.BeginTransactionAsync();
            try
            {
                // Create the Bill header
                var bill = new Bill
                {
                    ServiceRequestId = requestId,
                    TechnicianProfileId = tech.TechnicianProfileId,
                    TotalAmount = Math.Round(totalAmount, 2),
                    PaymentStatus = PaymentStatus.Unpaid,
                    CreatedAt = DateTime.UtcNow
                };

                await _uow.Bills.AddAsync(bill);

                // Save to get BillId (needed for BillItems FK)
                await _uow.SaveChangesAsync();

                // Create BillItem rows
                foreach (var item in validItems)
                {
                    var billItem = new BillItem
                    {
                        BillId = bill.Id,
                        Description = item.Description.Trim(),
                        Quantity = item.Quantity,
                        UnitPrice = Math.Round(item.UnitPrice, 2)
                    };

                    await _uow.Bills.AddItemAsync(billItem);
                }

                // Transition: Request → Billed
                request.Status = RequestStatus.Billed;
                request.UpdatedAt = DateTime.UtcNow;
                _uow.ServiceRequests.Update(request);

                // Log history
                var history = new ServiceHistory
                {
                    RequestId = requestId,
                    Status = RequestStatus.Billed,
                    ChangedByUserId = technicianUserId,
                    Note = $"Bill created by technician. " +
                                      $"Total: ₹{totalAmount:N2}. " +
                                      $"{validItems.Count} item(s).",
                    ChangedAt = DateTime.UtcNow
                };
                await _uow.ServiceHistories.AddAsync(history);

                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "Bill #{BillId} created for request #{RequestId}. " +
                    "Total: {Total}. Items: {Count}",
                    bill.Id, requestId, totalAmount, validItems.Count);

                // Return bill with items loaded
                var createdBill = await _uow.Bills
                    .GetWithItemsAndPaymentAsync(bill.Id);

                return Result<Bill>.Success(createdBill!);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Failed to create bill for request #{RequestId}",
                    requestId);
                return Result<Bill>.Failure(
                    "Failed to create bill. Please try again.");
            }
        }

        // =============================================================
        //  GET BILL BY REQUEST ID
        // =============================================================
        public async Task<Result<Bill>> GetBillByRequestIdAsync(
            int requestId)
        {
            var bill = await _uow.Bills
                .GetByRequestIdAsync(requestId);

            if (bill == null)
                return Result<Bill>.Failure(
                    "No bill found for this request.");

            return Result<Bill>.Success(bill);
        }

        // =============================================================
        //  GET BILL BY BILL ID
        // =============================================================
        public async Task<Result<Bill>> GetBillByIdAsync(int billId)
        {
            var bill = await _uow.Bills
                .GetWithItemsAndPaymentAsync(billId);

            if (bill == null)
                return Result<Bill>.Failure("Bill not found.");

            return Result<Bill>.Success(bill);
        }

        // =============================================================
        //  PAY BILL (Customer action)
        //  Marks bill as paid → Request → Completed → Tech → Available
        // =============================================================
        public async Task<Result<bool>> PayBillAsync(int billId, string customerId)
        {
            // Load bill
            var bill = await _uow.Bills.GetWithItemsAndPaymentAsync(billId);
            if (bill == null)
                return Result<bool>.Failure("Bill not found.");

            if (bill.PaymentStatus == PaymentStatus.Paid)
                return Result<bool>.Failure("This bill is already paid.");

            // Load the linked service request to verify ownership
            var request = await _uow.ServiceRequests
                .GetWithDetailsAsync(bill.ServiceRequestId);
            if (request == null)
                return Result<bool>.Failure("Service request not found.");

            if (request.CustomerId != customerId)
                return Result<bool>.Failure("You cannot pay this bill.");

            if (request.Status != RequestStatus.Billed)
                return Result<bool>.Failure(
                    $"Cannot pay — request is {request.Status}, not Billed.");

            await _uow.BeginTransactionAsync();
            try
            {
                // ── Mark bill paid ─────────────────────────────────────
                bill.PaymentStatus = PaymentStatus.Paid;
                bill.PaidAt = DateTime.UtcNow;
                _uow.Bills.Update(bill);

                // ── Move request to Completed ──────────────────────────
                request.Status = RequestStatus.Completed;
                request.UpdatedAt = DateTime.UtcNow;
                _uow.ServiceRequests.Update(request);

                // ── Free the technician back to Available ──────────────
                if (request.AssignedTechnicianProfileId.HasValue)
                {
                    var tech = await _uow.TechnicianProfiles
                        .GetByIdAsync(request.AssignedTechnicianProfileId.Value);

                    if (tech != null && tech.Status == TechnicianStatus.Busy)
                    {
                        tech.Status = TechnicianStatus.Available;
                        _uow.TechnicianProfiles.Update(tech);
                    }
                }

                // ── Log history ────────────────────────────────────────
                await _uow.ServiceHistories.AddAsync(new ServiceHistory
                {
                    RequestId = request.RequestId,
                    Status = RequestStatus.Completed,
                    ChangedByUserId = customerId,
                    Note = $"Payment received. " +
                                      $"Amount: ₹{bill.TotalAmount:N2}. " +
                                      $"Service completed.",
                    ChangedAt = DateTime.UtcNow
                });

                await _uow.CommitTransactionAsync();

                _logger.LogInformation(
                    "Bill #{BillId} paid by customer {CustomerId}. " +
                    "Request #{RequestId} completed. Technician freed.",
                    billId, customerId, request.RequestId);

                return Result<bool>.Success(true);
            }
            catch (Exception ex)
            {
                await _uow.RollbackTransactionAsync();
                _logger.LogError(ex,
                    "Payment failed for bill #{BillId}", billId);
                return Result<bool>.Failure(
                    "Payment processing failed. Please try again.");
            }
        }
    }
}