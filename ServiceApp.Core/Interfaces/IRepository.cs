// =================================================================
//  IRepository.cs — ServiceApp.Core/Interfaces
//
//  Generic repository interface. Every entity-specific repo
//  inherits from this and gets all basic CRUD operations free.
//
//  WHY REPOSITORY PATTERN?
//  - Services/controllers never write SQL or touch DbContext
//  - Swapping SQL Server for another DB = change one class
//  - Unit testing = mock IRepository<T>, no real DB needed
//
//  Expression<Func<T, bool>> = lambda predicate
//  Example: r => r.Status == RequestStatus.Pending
//  EF Core translates this to SQL: WHERE Status = 'Pending'
// =================================================================

using System.Linq.Expressions;

namespace ServiceApp.Core.Interfaces
{
    public interface IRepository<T> where T : class
    {
        // Get one entity by primary key (int Id)
        // EF checks in-memory cache first — efficient for re-loads
        Task<T?> GetByIdAsync(int id);

        // Get all rows — use carefully on large tables
        Task<IEnumerable<T>> GetAllAsync();

        // Filtered query — pass a lambda predicate
        // e.g. FindAsync(r => r.Status == RequestStatus.Pending)
        Task<IEnumerable<T>> FindAsync(Expression<Func<T, bool>> predicate);

        // First matching row or null
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

        // Stage entity for INSERT — not saved until SaveChangesAsync()
        Task AddAsync(T entity);

        // Stage entity for UPDATE — not saved until SaveChangesAsync()
        void Update(T entity);

        // Stage entity for DELETE — not saved until SaveChangesAsync()
        void Remove(T entity);

        // EXISTS check — more efficient than loading the full entity
        // Translates to: SELECT CASE WHEN EXISTS(...) THEN 1 ELSE 0
        Task<bool> AnyAsync(Expression<Func<T, bool>> predicate);

        // COUNT — translates to: SELECT COUNT(*) WHERE ...
        Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null);
    }
}