// =================================================================
//  IBillService.cs — ServiceApp.Core/Interfaces
//
//  Contract for all billing business logic.
//
//  BILLING FLOW:
//  1. Technician finishes the job (status = InProgress)
//  2. Technician creates a bill with line items
//  3. System calculates total, sets status → Billed
//  4. Customer receives notification (Phase 3+)
//  5. Customer pays → webhook fires → status → Completed
//
//  Bill can only be created once per request.
//  TotalAmount = sum of all BillItem amounts.
// =================================================================

using ServiceApp.Core.Common;
using ServiceApp.Core.Entities;

namespace ServiceApp.Core.Interfaces
{
    public interface IBillService
    {
        // Create a bill for a completed job.
        // Validates: request must be InProgress, technician must
        // be the assigned one, no existing bill for this request.
        // Side effects:
        //   - Bill row created with all BillItems
        //   - Request.Status → Billed
        //   - ServiceHistory row inserted
        Task<Result<Bill>> CreateBillAsync(
            int requestId,
            string technicianUserId,
            List<BillItemInput> items);

        // Load bill with all items and payment info.
        // Used by customer (to pay) and technician (to view).
        Task<Result<Bill>> GetBillByRequestIdAsync(int requestId);

        // Load bill by its own ID.
        Task<Result<Bill>> GetBillByIdAsync(int billId);
    }

    // Simple DTO carrying one line item from the form.
    // Not an entity — just a data transfer object.
    public class BillItemInput
    {
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
    }
}