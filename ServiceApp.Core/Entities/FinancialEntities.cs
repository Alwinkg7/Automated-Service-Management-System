// =================================================================
//  FinancialEntities.cs — ServiceApp.Core/Entities
//
//  Three entities that handle billing and payment:
//
//  Bill      → created by technician after work (header)
//  BillItem  → line items inside a bill (labour, parts etc.)
//  Payment   → created after customer pays via gateway
//
//  EXAMPLE:
//  Bill #12 for Request #5 — Total ₹850
//    BillItem: "Labour charges"     Qty:1  Price:₹500  → ₹500
//    BillItem: "Copper wire (2m)"   Qty:2  Price:₹120  → ₹240
//    BillItem: "Service visit fee"  Qty:1  Price:₹110  → ₹110
//  Payment #7 — ₹850 via Razorpay — txn_abc123
// =================================================================

using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Entities
{
    // ----------------------------------------------------------
    //  Bill — the invoice header
    // ----------------------------------------------------------
    public class Bill
    {
        public int Id { get; set; }
        public int ServiceRequestId { get; set; }
        public int TechnicianProfileId { get; set; }
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;
        public decimal LaborCost { get; set; }
        public decimal MaterialCost { get; set; }
        public decimal TotalAmount { get; set; }
        public string Description { get; set; } = string.Empty;
        public bool IsPaid { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }

        // Navigation
        public ServiceRequest ServiceRequest { get; set; } = null!;
        public TechnicianProfile Technician { get; set; } = null!; 
        public ICollection<BillItem> BillItems { get; set; } = new List<BillItem>();
        public Payment? Payment { get; set; }          
    }

    // ----------------------------------------------------------
    //  BillItem — one line on the invoice
    // ----------------------------------------------------------
    public class BillItem
    {
        public int BillItemId { get; set; }
        public int BillId { get; set; }
        public virtual Bill Bill { get; set; } = null!;
        public string Description { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal UnitPrice { get; set; }
        public decimal Amount => Quantity * UnitPrice;
    }

    // ----------------------------------------------------------
    //  Payment — created after gateway confirms payment
    //
    //  IMPORTANT FLOW:
    //  1. Customer clicks Pay → we create a Razorpay order
    //  2. Customer completes payment on Razorpay's page
    //  3. Razorpay calls our webhook: POST /api/payments/webhook
    //  4. We verify the signature (reject if unsigned)
    //  5. We check GatewayTransactionId — already processed? Skip.
    //  6. We create this Payment row + mark Bill as Paid
    //
    //  Never trust the frontend saying "payment done" —
    //  always wait for the signed webhook from the gateway.
    // ----------------------------------------------------------
    public class Payment
    {
        public int PaymentId { get; set; }
        public int BillId { get; set; }
        public virtual Bill Bill { get; set; } = null!;
        public decimal Amount { get; set; }
        public string PaymentMethod { get; set; } = string.Empty;
        public string? GatewayTransactionId { get; set; }
        public string? GatewayOrderId { get; set; }
        public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    }
}