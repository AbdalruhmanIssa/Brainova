using Brainova.DAL.Data;
using Brainova.DAL.Repositories.Interface;
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

        public void Dispose() => _db.Dispose();
    }
}
