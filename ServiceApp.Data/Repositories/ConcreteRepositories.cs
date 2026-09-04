// =================================================================
//  ConcreteRepositories.cs — ServiceApp.Data/Repositories
//
//  Concrete implementations of all entity-specific interfaces.
//  Each class:
//  1. Inherits Repository<T> → gets all basic CRUD free
//  2. Implements its specific interface → adds custom queries
//
//  Include() = SQL JOIN — loads related entities in ONE query.
//  Without Include(), navigation properties are NULL.
//
//  ThenInclude() = JOIN on a JOIN
//  e.g. Include(r => r.AssignedTechnician)     ← join technician
//           .ThenInclude(t => t.User)           ← join user from tech
//  Result: request + technician + technician's user info in 1 query
// =================================================================

using Microsoft.EntityFrameworkCore;
using ServiceApp.Core.Entities;
using ServiceApp.Core.Enums;
using ServiceApp.Core.Interfaces;
using ServiceApp.Data.Context;

namespace ServiceApp.Data.Repositories
{
    // ── UserRepository ────────────────────────────────────────────
    public class UserRepository : Repository<ApplicationUser>, IUserRepository
    {
        public UserRepository(ApplicationDbContext context) : base(context) { }

        public async Task<ApplicationUser?> GetByIdAsync(string id) =>
        await _dbSet.FindAsync(id);

        public async Task<ApplicationUser?> GetByEmailAsync(string email) =>
            await _dbSet.FirstOrDefaultAsync(u => u.Email == email);

        // Load user with whichever profile exists
        // Uses conditional Include via AsSplitQuery for performance
        public async Task<ApplicationUser?> GetWithProfileAsync(string userId) =>
            await _dbSet
                .Include(u => u.CustomerProfile)
                .Include(u => u.TechnicianProfile)
                .Include(u => u.AdminProfile)
                .FirstOrDefaultAsync(u => u.Id == userId);

        public async Task<IEnumerable<ApplicationUser>> GetAllByRoleAsync(UserRole role) =>
            await _dbSet
                .Where(u => u.Role == role && u.IsActive)
                .OrderBy(u => u.FullName)
                .AsNoTracking()
                .ToListAsync();
    }

    // ── CustomerProfileRepository ─────────────────────────────────
    public class CustomerProfileRepository
        : Repository<CustomerProfile>, ICustomerProfileRepository
    {
        public CustomerProfileRepository(ApplicationDbContext context)
            : base(context) { }

        public async Task<CustomerProfile?> GetByUserIdAsync(string userId) =>
            await _dbSet
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.UserId == userId);
    }

    // ── TechnicianProfileRepository ───────────────────────────────
    public class TechnicianProfileRepository
        : Repository<TechnicianProfile>, ITechnicianProfileRepository
    {
        public TechnicianProfileRepository(ApplicationDbContext context)
            : base(context) { }

        public async Task<TechnicianProfile?> GetByUserIdAsync(string userId) =>
            await _dbSet
                .FirstOrDefaultAsync(t => t.UserId == userId);

        // AUTO-ASSIGNMENT ENGINE QUERY
        // Only Available technicians with matching skill.
        // Ordered by Rating desc → best rated technician assigned first.
        public async Task<IEnumerable<TechnicianProfile>> GetAvailableBySkillAsync(
            ServiceCategory skill) =>
            await _dbSet
                .Where(t => t.Status == TechnicianStatus.Available
                         && t.Skill == skill)
                .OrderByDescending(t => t.Rating)
                .Include(t => t.User)
                .AsNoTracking()
                .ToListAsync();

        public async Task<IEnumerable<TechnicianProfile>> GetAllWithUsersAsync() =>
            await _dbSet
                .Include(t => t.User)
                .OrderByDescending(t => t.TotalJobsCompleted)
                .AsNoTracking()
                .ToListAsync();

        public async Task<TechnicianProfile?> GetWithUserAsync(int id) =>
            await _dbSet
                .Include(t => t.User)
                .FirstOrDefaultAsync(t => t.TechnicianProfileId == id);
    }

    // ── AdminProfileRepository ────────────────────────────────────
    public class AdminProfileRepository
        : Repository<AdminProfile>, IAdminProfileRepository
    {
        public AdminProfileRepository(ApplicationDbContext context)
            : base(context) { }

        public async Task<AdminProfile?> GetByUserIdAsync(string userId) =>
            await _dbSet
                .Include(a => a.User)
                .FirstOrDefaultAsync(a => a.UserId == userId);
    }

    // ── ServiceRequestRepository ──────────────────────────────────
    public class ServiceRequestRepository
        : Repository<ServiceRequest>, IServiceRequestRepository
    {
        public ServiceRequestRepository(ApplicationDbContext context)
            : base(context) { }

        // Customer "My Requests" — load with technician name for display
        public async Task<IEnumerable<ServiceRequest>> GetByCustomerIdAsync(
            string customerId) =>
            await _dbSet
                .Where(r => r.CustomerId == customerId)
                .OrderByDescending(r => r.CreatedAt)
                .Include(r => r.AssignedTechnician)
                    .ThenInclude(t => t!.User)
                .AsNoTracking()
                .ToListAsync();

        // Technician "My Jobs" — load with customer info so tech can call them
        public async Task<IEnumerable<ServiceRequest>> GetByTechnicianIdAsync(
            int technicianProfileId) =>
            await _dbSet
                .Where(r => r.AssignedTechnicianProfileId == technicianProfileId)
                .OrderByDescending(r => r.CreatedAt)
                .Include(r => r.Customer)
                .AsNoTracking()
                .ToListAsync();

        // Admin filtered views
        public async Task<IEnumerable<ServiceRequest>> GetByStatusAsync(
            RequestStatus status) =>
            await _dbSet
                .Where(r => r.Status == status)
                .OrderByDescending(r => r.CreatedAt)
                .Include(r => r.Customer)
                .Include(r => r.AssignedTechnician)
                    .ThenInclude(t => t!.User)
                .AsNoTracking()
                .ToListAsync();

        // Request Details — loads everything in ONE query (multiple JOINs)
        // Used on the details page where we show all related data
        public async Task<ServiceRequest?> GetWithDetailsAsync(int requestId) =>
            await _dbSet
                .Include(r => r.Customer)
                    .ThenInclude(c => c.CustomerProfile)
                .Include(r => r.AssignedTechnician)
                    .ThenInclude(t => t!.User)
                .Include(r => r.History)
                .Include(r => r.Bill)
                    .ThenInclude(b => b!.BillItems)
                .Include(r => r.Bill)
                    .ThenInclude(b => b!.Payment)
                .FirstOrDefaultAsync(r => r.RequestId == requestId);

        // Auto-assignment: oldest Pending request first (fair queue)
        // No AsNoTracking — we will modify Status on these
        public async Task<IEnumerable<ServiceRequest>> GetPendingOrderedByDateAsync() =>
            await _dbSet
                .Where(r => r.Status == RequestStatus.Pending)
                .OrderBy(r => r.CreatedAt)
                .Include(r => r.Customer)
                .ToListAsync();

        // Add this method — mirrors GetByStatusAsync but without the WHERE clause
        public async Task<IEnumerable<ServiceRequest>> GetAllWithDetailsAsync() =>
            await _dbSet
                .OrderByDescending(r => r.CreatedAt)
                .Include(r => r.Customer)
                .Include(r => r.AssignedTechnician)
                    .ThenInclude(t => t!.User)
                .AsNoTracking()
                .ToListAsync();
    }

    // ── ServiceHistoryRepository ──────────────────────────────────
    public class ServiceHistoryRepository
        : Repository<ServiceHistory>, IServiceHistoryRepository
    {
        public ServiceHistoryRepository(ApplicationDbContext context)
            : base(context) { }

        // Timeline ordered oldest first — so page shows
        // "Pending → Assigned → InProgress → ..." in correct order
        public async Task<IEnumerable<ServiceHistory>> GetByRequestIdAsync(
            int requestId) =>
            await _dbSet
                .Where(h => h.RequestId == requestId)
                .OrderBy(h => h.ChangedAt)
                .AsNoTracking()
                .ToListAsync();
    }

    // ── BillRepository ────────────────────────────────────────────
    public class BillRepository : Repository<Bill>, IBillRepository
    {
        public BillRepository(ApplicationDbContext context) : base(context) { }

        // Load bill with line items — for technician edit/create bill page
        public async Task<Bill?> GetByRequestIdAsync(int requestId) =>
            await _dbSet
                .Include(b => b.BillItems)
                .FirstOrDefaultAsync(b => b.ServiceRequestId == requestId);

        // Full detail — for customer "View Bill" and "Pay" pages
        public async Task<Bill?> GetWithItemsAndPaymentAsync(int billId) =>
            await _dbSet
                .Include(b => b.BillItems)
                .Include(b => b.Payment)
                .Include(b => b.Technician)
                    .ThenInclude(t => t.User)
                .FirstOrDefaultAsync(b => b.Id == billId);
        public async Task AddItemAsync(BillItem item) =>
        await _context.BillItems.AddAsync(item);
    }

    // ── PaymentRepository ─────────────────────────────────────────
    public class PaymentRepository : Repository<Payment>, IPaymentRepository
    {
        public PaymentRepository(ApplicationDbContext context) : base(context) { }

        public async Task<Payment?> GetByBillIdAsync(int billId) =>
            await _dbSet.FirstOrDefaultAsync(p => p.BillId == billId);

        // Idempotency check in webhook handler
        // If record found → payment already processed → skip
        public async Task<Payment?> GetByGatewayTransactionIdAsync(
            string transactionId) =>
            await _dbSet.FirstOrDefaultAsync(
                p => p.GatewayTransactionId == transactionId);
    }
}