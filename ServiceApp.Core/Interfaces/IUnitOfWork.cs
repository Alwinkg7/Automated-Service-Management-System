// =================================================================
//  IUnitOfWork.cs — ServiceApp.Core/Interfaces
//
//  Groups all repositories + one shared DbContext so that
//  multi-step operations run inside a single DB transaction.
//
//  EXAMPLE — completing a job (5 steps, must ALL succeed):
//    await _uow.BeginTransactionAsync();
//    try
//    {
//        bill.PaymentStatus = PaymentStatus.Paid;           // step 1
//        request.Status = RequestStatus.Completed;          // step 2
//        techProfile.Status = TechnicianStatus.Available;   // step 3
//        await _uow.Payments.AddAsync(payment);             // step 4
//        await _uow.ServiceHistories.AddAsync(history);     // step 5
//        await _uow.CommitTransactionAsync();  // all 5 committed together
//    }
//    catch
//    {
//        await _uow.RollbackTransactionAsync(); // all 5 rolled back
//        throw;
//    }
//
//  Without UoW: each repo would have its own DbContext and
//  SaveChanges — steps 1-3 could succeed while 4-5 fail,
//  leaving the database in a broken half-updated state.
// =================================================================

namespace ServiceApp.Core.Interfaces
{
    public interface IUnitOfWork : IDisposable
    {
        // All repos share ONE DbContext instance
        // This is what makes cross-repo transactions possible
        IUserRepository Users { get; }
        ICustomerProfileRepository CustomerProfiles { get; }
        ITechnicianProfileRepository TechnicianProfiles { get; }
        IAdminProfileRepository AdminProfiles { get; }
        IServiceRequestRepository ServiceRequests { get; }
        IServiceHistoryRepository ServiceHistories { get; }
        IBillRepository Bills { get; }
        IPaymentRepository Payments { get; }

        // Save all pending changes (INSERTs, UPDATEs, DELETEs)
        // Use this for simple single-step operations
        Task<int> SaveChangesAsync();

        // Use these three for multi-step operations that must be atomic
        Task BeginTransactionAsync();
        Task CommitTransactionAsync();    // saves + finalizes
        Task RollbackTransactionAsync();  // undoes everything since Begin
    }
}