// =================================================================
//  UnitOfWork.cs — ServiceApp.Data
//
//  Owns ONE DbContext and shares it across ALL repositories.
//  This is the key — same DbContext = same transaction = atomic.
//
//  LAZY INITIALIZATION (??= operator):
//  Repositories are created only when first accessed.
//  "If null → create it; otherwise return existing instance"
//  All repos receive the same _context so they share state.
//
//  LIFETIME: Registered as Scoped in Program.cs
//  = One UnitOfWork per HTTP request, disposed at request end.
//  EF Core DbContext is also Scoped — they match perfectly.
// =================================================================

using Microsoft.EntityFrameworkCore.Storage;
using ServiceApp.Core.Interfaces;
using ServiceApp.Data.Context;
using ServiceApp.Data.Repositories;

namespace ServiceApp.Data
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly ApplicationDbContext _context;
        private IDbContextTransaction? _transaction;

        // Lazily initialized — only created when first accessed
        private IUserRepository? _users;
        private ICustomerProfileRepository? _customerProfiles;
        private ITechnicianProfileRepository? _technicianProfiles;
        private IAdminProfileRepository? _adminProfiles;
        private IServiceRequestRepository? _serviceRequests;
        private IServiceHistoryRepository? _serviceHistories;
        private IBillRepository? _bills;
        private IPaymentRepository? _payments;

        public UnitOfWork(ApplicationDbContext context)
        {
            _context = context;
        }

        // ??= means: "if null, create and assign; else return existing"
        // All repos pass the SAME _context → same transaction boundary
        public IUserRepository Users =>
            _users ??= new UserRepository(_context);

        public ICustomerProfileRepository CustomerProfiles =>
            _customerProfiles ??= new CustomerProfileRepository(_context);

        public ITechnicianProfileRepository TechnicianProfiles =>
            _technicianProfiles ??= new TechnicianProfileRepository(_context);

        public IAdminProfileRepository AdminProfiles =>
            _adminProfiles ??= new AdminProfileRepository(_context);

        public IServiceRequestRepository ServiceRequests =>
            _serviceRequests ??= new ServiceRequestRepository(_context);

        public IServiceHistoryRepository ServiceHistories =>
            _serviceHistories ??= new ServiceHistoryRepository(_context);

        public IBillRepository Bills =>
            _bills ??= new BillRepository(_context);

        public IPaymentRepository Payments =>
            _payments ??= new PaymentRepository(_context);

        // Flush all pending changes to the database
        // Returns number of rows affected
        public async Task<int> SaveChangesAsync() =>
            await _context.SaveChangesAsync();

        // Start a DB transaction
        // All subsequent changes are tentative until Commit or Rollback
        public async Task BeginTransactionAsync() =>
            _transaction = await _context.Database.BeginTransactionAsync();

        // Save all changes AND finalize the transaction atomically
        public async Task CommitTransactionAsync()
        {
            if (_transaction == null)
                throw new InvalidOperationException(
                    "No transaction started. Call BeginTransactionAsync first.");

            await _context.SaveChangesAsync();
            await _transaction.CommitAsync();
            await _transaction.DisposeAsync();
            _transaction = null;
        }

        // Undo ALL changes since BeginTransactionAsync
        // Call this in the catch block of any multi-step operation
        public async Task RollbackTransactionAsync()
        {
            if (_transaction != null)
            {
                await _transaction.RollbackAsync();
                await _transaction.DisposeAsync();
                _transaction = null;
            }
        }

        // Called automatically at end of HTTP request (Scoped lifetime)
        public void Dispose()
        {
            _transaction?.Dispose();
            _context.Dispose();
        }
    }
}