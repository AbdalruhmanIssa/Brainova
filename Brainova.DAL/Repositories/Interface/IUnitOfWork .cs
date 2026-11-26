

namespace Brainova.DAL.Repositories.Interface
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<TEntity> Repo<TEntity>() where TEntity : class;
        Task<int> SaveChangesAsync(CancellationToken ct = default);

        // Runs the operation inside a DB transaction, wrapped in EF's execution
        // strategy so it works with EnableRetryOnFailure. If a transient SQL error
        // happens, the WHOLE operation (reads + writes) is retried as one unit.
        Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<Task<TResult>> operation,
            CancellationToken ct = default);
    }
}
