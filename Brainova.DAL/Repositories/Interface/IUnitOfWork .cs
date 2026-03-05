
namespace Brainova.DAL.Repositories.Interface
{
    public interface IUnitOfWork : IDisposable
    {
        IGenericRepository<TEntity> Repo<TEntity>() where TEntity : class;
        Task<int> SaveChangesAsync(CancellationToken ct = default);
    }
}
