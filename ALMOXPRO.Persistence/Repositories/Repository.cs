using ALMOXPRO.Application.Interfaces;
using ALMOXPRO.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;

namespace ALMOXPRO.Persistence.Repositories;

public class Repository<T> : IRepository<T> where T : class
{
    protected readonly AlmoxProDbContext Context;
    protected readonly DbSet<T> Set;

    public Repository(AlmoxProDbContext context)
    {
        Context = context;
        Set = context.Set<T>();
    }

    public virtual Task<T?> GetByIdAsync(int id, CancellationToken ct = default) =>
        Set.FindAsync([id], ct).AsTask();

    public virtual Task<List<T>> GetAllAsync(CancellationToken ct = default) =>
        Set.ToListAsync(ct);

    public Task<List<T>> FindAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Set.Where(predicate).ToListAsync(ct);

    public Task<bool> AnyAsync(Expression<Func<T, bool>> predicate, CancellationToken ct = default) =>
        Set.AnyAsync(predicate, ct);

    public Task<int> CountAsync(Expression<Func<T, bool>>? predicate = null, CancellationToken ct = default) =>
        predicate is null ? Set.CountAsync(ct) : Set.CountAsync(predicate, ct);

    public async Task AddAsync(T entity, CancellationToken ct = default) =>
        await Set.AddAsync(entity, ct);

    public void Update(T entity) => Set.Update(entity);

    public void Remove(T entity) => Set.Remove(entity);
}
