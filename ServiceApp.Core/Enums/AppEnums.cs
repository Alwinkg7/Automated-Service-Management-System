// =================================================================
//  AppEnums.cs — ServiceApp.Core/Enums
//
//  All enums for the entire application live here.
//  Stored as STRINGS in the database (not numbers) so the
//  SQL table is human-readable. e.g. Status = 'Pending'
//  instead of Status = 0. Much easier to debug.
// =================================================================

namespace ServiceApp.Core.Enums
{
    // ── Who is this user? ─────────────────────────────────────────
    // Stored on ApplicationUser.Role column.
    // Controls which Area dashboard they land on after login.
    public enum UserRole
    {
        Customer,     // books services, pays bills
        Technician,   // accepts jobs, creates bills
        Admin         // manages the whole platform
    }

    // ── Technician availability ───────────────────────────────────
    // Changes automatically during the job lifecycle:
    //   Available → Busy    (when technician accepts a job)
    //   Busy → Available    (when job is marked completed)
    // Technician can also manually toggle Available ↔ Offline
    public enum TechnicianStatus
    {
        Available,   // ready to receive new job assignments
        Busy,        // currently working a job — no new assignments
        Offline      // manually gone off-duty (like a driver in Rapido)
    }

    // ── Job lifecycle ─────────────────────────────────────────────
    // A ServiceRequest moves through these states in strict order.
    // The service layer enforces valid transitions — you cannot
    // skip from Pending directly to Completed for example.
    //
    // Full flow:
    // Pending → Assigned → InProgress → Billed → Completed
    //                                          ↘ Cancelled (any time before Completed)
    public enum RequestStatus
    {
        Pending,      // customer created the request, no technician yet
        Assigned,     // admin assigned a technician, waiting for acceptance
        InProgress,   // technician accepted, work has started
        Billed,       // technician finished and created the bill
        Completed,    // customer paid — the final successful state
        Cancelled     // cancelled before completion
    }

    // ── Bill payment state ────────────────────────────────────────
    public enum PaymentStatus
    {
        Unpaid,    // bill exists but customer hasn't paid
        Paid,      // payment confirmed by gateway webhook
        Refunded   // payment reversed (Phase 3+)
    }

    // ── What kind of service? ─────────────────────────────────────
    // Used in two places:
    // 1. TechnicianProfile.Skill — what the technician offers
    // 2. ServiceRequest.Category — what the customer needs
    // The auto-assignment engine matches these two fields.
    public enum ServiceCategory
    {
        Electrician,
        Plumber,
        Carpenter,
        Painter,
        Cleaner,
        ACTechnician,
        Mechanic,
        Other
    }
}