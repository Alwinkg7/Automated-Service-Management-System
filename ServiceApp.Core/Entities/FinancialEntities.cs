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
        public int BillId { get; set; }

        // One bill per request — enforced by unique index in DbContext
        public int RequestId { get; set; }
        public virtual ServiceRequest Request { get; set; } = null!;

        // The technician who created this bill
        public int TechnicianProfileId { get; set; }
        public virtual TechnicianProfile Technician { get; set; } = null!;

        // Sum of all BillItem amounts.
        // Calculated in the service layer, stored here so we don't
        // recalculate on every page load.
        public decimal TotalAmount { get; set; }

        // Starts Unpaid. Moves to Paid after gateway webhook confirms.
        public PaymentStatus PaymentStatus { get; set; } = PaymentStatus.Unpaid;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? PaidAt { get; set; }  // stamped when PaymentStatus → Paid

        // ── Navigation ────────────────────────────────────────────

        public virtual ICollection<BillItem> BillItems { get; set; }
            = new List<BillItem>();

        // The payment record — null until customer pays
        public virtual Payment? Payment { get; set; }
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

        // Computed property — NOT stored in DB.
        // EF is told to ignore this via .Ignore() in DbContext.
        // Calculated on the fly: Quantity × UnitPrice
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

        // "Razorpay", "Stripe", "Cash" — set based on how customer paid
        public string PaymentMethod { get; set; } = string.Empty;

        // Transaction ID from Razorpay/Stripe.
        // Has a UNIQUE INDEX in DB — this is how we prevent
        // double-processing if the gateway fires the webhook twice.
        public string? GatewayTransactionId { get; set; }

        // Order ID created on the gateway before payment starts
        public string? GatewayOrderId { get; set; }

        public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    }
}