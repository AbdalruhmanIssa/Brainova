using Brainova.DAL.Data;
using Brainova.DAL.Repositories.Interface;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace Brainova.DAL.Repositories.Classes
{
    public class UnitOfWork : IUnitOfWork
    {
        private readonly AppDbContext _db;
        private readonly Dictionary<Type, object> _repos = new();

        public UnitOfWork(AppDbContext db)
        {
            _db = db;
        }
        //SELECT * FROM ReportQuestions
        public IGenericRepository<TEntity> Repo<TEntity>() where TEntity : class
        {
            var type = typeof(TEntity);

            if (_repos.TryGetValue(type, out var repo))
                return (IGenericRepository<TEntity>)repo;

            var newRepo = new GenericRepository<TEntity>(_db);
            _repos[type] = newRepo;
            return newRepo;
        }

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
            => _db.SaveChangesAsync(ct);

        public Task<TResult> ExecuteInTransactionAsync<TResult>(
            Func<Task<TResult>> operation,
            CancellationToken ct = default)
        {
            var strategy = _db.Database.CreateExecutionStrategy();
            var attempt = 0;

            return strategy.ExecuteAsync(async () =>
            {
                // On a retry, drop entities tracked by the failed attempt so they
                // aren't inserted twice.
                if (attempt++ > 0)
                    _db.ChangeTracker.Clear();

                await using var transaction = await _db.Database.BeginTransactionAsync(ct);
                var result = await operation();
                await transaction.CommitAsync(ct);
                return result;
            });
        }

        public void Dispose() => _db.Dispose();
    }
}
