// =================================================================
//  IRepositories.cs — ServiceApp.Core/Interfaces
//
//  Entity-specific repository interfaces.
//  Each extends IRepository<T> with custom queries that only
//  make sense for that particular entity.
//
//  These interfaces are what the SERVICE LAYER depends on.
//  The DATA LAYER provides the concrete implementations.
//  This separation means: services don't know HOW data is fetched,
//  only WHAT they need.
// =================================================================

using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;

namespace ServiceApp.Core.Interfaces
{
    // ── Users ─────────────────────────────────────────────────────
    public interface IUserRepository : IRepository<ApplicationUser>
    {
        // Find user by email — used during login validation
        Task<ApplicationUser?> GetByEmailAsync(string email);

        // Load user with their profile in one query
        // Avoids a second round-trip to load the profile separately
        Task<ApplicationUser?> GetWithProfileAsync(string userId);

        // Admin "All Users" management list
        Task<IEnumerable<ApplicationUser>> GetAllByRoleAsync(UserRole role);
        Task<ApplicationUser?> GetByIdAsync(string customerId);
    }

    // ── CustomerProfile ───────────────────────────────────────────
    public interface ICustomerProfileRepository : IRepository<CustomerProfile>
    {
        // Load profile by UserId (called right after login)
        Task<CustomerProfile?> GetByUserIdAsync(string userId);
    }

    // ── TechnicianProfile ─────────────────────────────────────────
    public interface ITechnicianProfileRepository : IRepository<TechnicianProfile>
    {
        // Load profile by UserId (called right after technician login)
        Task<TechnicianProfile?> GetByUserIdAsync(string userId);

        // THE most critical query in the system.
        // Used by auto-assignment engine:
        // "Find me all Available technicians who can do Plumbing"
        // Ordered by Rating descending — best rated gets priority
        Task<IEnumerable<TechnicianProfile>> GetAvailableBySkillAsync(ServiceCategory skill);

        // Admin "All Technicians" list with user info for display
        Task<IEnumerable<TechnicianProfile>> GetAllWithUsersAsync();

        // Load profile with full user info included
        Task<TechnicianProfile?> GetWithUserAsync(int technicianProfileId);
    }

    // ── AdminProfile ──────────────────────────────────────────────
    public interface IAdminProfileRepository : IRepository<AdminProfile>
    {
        Task<AdminProfile?> GetByUserIdAsync(string userId);
    }

    // ── ServiceRequest ────────────────────────────────────────────
    public interface IServiceRequestRepository : IRepository<ServiceRequest>
    {
        // Customer "My Requests" page
        Task<IEnumerable<ServiceRequest>> GetByCustomerIdAsync(string customerId);

        // Technician "My Jobs" page
        Task<IEnumerable<ServiceRequest>> GetByTechnicianIdAsync(int technicianProfileId);

        // Admin filtered views — "All Pending", "All InProgress" etc.
        Task<IEnumerable<ServiceRequest>> GetByStatusAsync(RequestStatus status);

        // Request Details page — loads EVERYTHING in one SQL query:
        // Customer, assigned technician + user, history, bill + items + payment
        Task<ServiceRequest?> GetWithDetailsAsync(int requestId);

        // Auto-assignment engine: oldest unassigned request first
        // Fair queue — first come, first served
        Task<IEnumerable<ServiceRequest>> GetPendingOrderedByDateAsync();
        Task<IEnumerable<ServiceRequest>> GetAllWithDetailsAsync();
    }

    // ── ServiceHistory ────────────────────────────────────────────
    public interface IServiceHistoryRepository : IRepository<ServiceHistory>
    {
        // Full timeline for one request — ordered oldest first
        // Displayed as a step-by-step timeline on details page
        Task<IEnumerable<ServiceHistory>> GetByRequestIdAsync(int requestId);
    }

    // ── Bill ──────────────────────────────────────────────────────
    public interface IBillRepository : IRepository<Bill>
    {
        // Load bill for a request including line items
        Task<Bill?> GetByRequestIdAsync(int requestId);

        // Full bill with items + payment — for View Bill / Pay pages
        Task<Bill?> GetWithItemsAndPaymentAsync(int billId);
        Task AddItemAsync(BillItem item);
    }

    // ── Payment ───────────────────────────────────────────────────
    public interface IPaymentRepository : IRepository<Payment>
    {
        Task<Payment?> GetByBillIdAsync(int billId);

        // IDEMPOTENCY CHECK — used in payment webhook handler.
        // Before processing: did we already record this transaction?
        // Gateways retry webhooks on timeout — we must not double-process.
        Task<Payment?> GetByGatewayTransactionIdAsync(string transactionId);
    }
}