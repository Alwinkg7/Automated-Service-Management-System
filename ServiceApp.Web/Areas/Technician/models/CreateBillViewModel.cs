// =================================================================
//  CreateBillViewModel.cs
//
//  Powers the "Create Bill" form.
//  The technician adds dynamic line items (labour, parts, etc.)
//  and the total is calculated live in the browser via JavaScript.
//
//  FORM STRUCTURE:
//  - Request info (read-only display at top)
//  - Dynamic rows: Description | Qty | Unit Price | Amount | Remove
//  - "Add item" button adds a new row (JS)
//  - Total calculated live (JS)
//  - Submit sends all rows as a list
// =================================================================

using ServiceApp.Core.Entities;

namespace ServiceApp.Web.Areas.Technician.Models
{
    public class CreateBillViewModel
    {
        // Request being billed — shown read-only at top
        public ServiceRequest Request { get; set; } = null!;

        // Bill line items — dynamically added by the technician
        // Posted as a list: Items[0].Description, Items[0].Quantity etc.
        public List<BillItemRow> Items { get; set; }
            = new List<BillItemRow>
            {
                // Start with one empty row so the form isn't blank
                new BillItemRow()
            };
    }

    public class BillItemRow
    {
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; } = 0;
    }
}