using Rating.Application.Abstractions;
using Rating.Infrastructure.Persistence;

namespace Rating.Infrastructure.Persistence;

internal sealed class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken ct = default) => db.SaveChangesAsync(ct);
}
