// =================================================================
//  Repository.cs — ServiceApp.Data/Repositories
//
//  Concrete base implementation of IRepository<T>.
//  All specific repositories inherit this — they get
//  all basic CRUD operations without writing a single line.
//
//  KEY CONCEPT — AsNoTracking():
//  When you query with AsNoTracking(), EF does NOT watch the
//  returned objects for changes. This is faster for read-only
//  queries (list pages, dashboards) because EF skips the
//  overhead of change detection.
//
//  WITHOUT AsNoTracking (tracking ON):
//  EF watches the object. If you modify it and call
//  SaveChangesAsync(), EF detects the change and generates
//  an UPDATE. Use this when you WILL modify the entity.
//
//  Rule of thumb:
//  - Reading for display only   → AsNoTracking() ✓
//  - Reading to modify and save → No AsNoTracking ✓
// =================================================================

using Microsoft.EntityFrameworkCore;
using ServiceApp.Core.Interfaces;
using ServiceApp.Data.Context;
using System.Linq.Expressions;

namespace ServiceApp.Data.Repositories
{
    public class Repository<T> : IRepository<T> where T : class
    {
        // Protected so child repositories can access them
        // when building complex queries with Include(), OrderBy() etc.
        protected readonly ApplicationDbContext _context;
        protected readonly DbSet<T> _dbSet;

        public Repository(ApplicationDbContext context)
        {
            _context = context;
            // Set<T>() returns the DbSet for type T
            // If T = ServiceRequest → this is context.ServiceRequests
            _dbSet = context.Set<T>();
        }

        // FindAsync by primary key
        // EF checks its in-memory identity map first before hitting SQL
        public async Task<T?> GetByIdAsync(int id) =>
            await _dbSet.FindAsync(id);

        // Get all rows — AsNoTracking for read-only performance
        public async Task<IEnumerable<T>> GetAllAsync() =>
            await _dbSet.AsNoTracking().ToListAsync();

        // Filtered query — EF translates the lambda to a WHERE clause
        // AsNoTracking: these are for display, won't be modified
        public async Task<IEnumerable<T>> FindAsync(
            Expression<Func<T, bool>> predicate) =>
            await _dbSet.AsNoTracking().Where(predicate).ToListAsync();

        // First match or null
        // No AsNoTracking — we might modify this entity
        public async Task<T?> FirstOrDefaultAsync(
            Expression<Func<T, bool>> predicate) =>
            await _dbSet.FirstOrDefaultAsync(predicate);

        // Stage for INSERT — DbContext tracks this as "Added"
        // NOT saved until SaveChangesAsync() is called
        public async Task AddAsync(T entity) =>
            await _dbSet.AddAsync(entity);

        // Stage for UPDATE — DbContext marks entity as "Modified"
        // NOT saved until SaveChangesAsync() is called
        public void Update(T entity) =>
            _dbSet.Update(entity);

        // Stage for DELETE — DbContext marks entity as "Deleted"
        // NOT saved until SaveChangesAsync() is called
        public void Remove(T entity) =>
            _dbSet.Remove(entity);

        // Existence check — more efficient than loading full entity
        public async Task<bool> AnyAsync(
            Expression<Func<T, bool>> predicate) =>
            await _dbSet.AnyAsync(predicate);

        // COUNT with optional filter
        public async Task<int> CountAsync(
            Expression<Func<T, bool>>? predicate = null) =>
            predicate == null
                ? await _dbSet.CountAsync()
                : await _dbSet.CountAsync(predicate);
    }
}