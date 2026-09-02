using Tenant.Domain.Persistence;

namespace Tenant.Infrastructure.Persistence;

public class UnitOfWork(AppDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return db.SaveChangesAsync(cancellationToken);
    }
}