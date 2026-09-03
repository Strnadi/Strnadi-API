using Administration.Domain.Persistence;

namespace Administration.Infrastructure.Persistence;

public class UnitOfWork(AdminDbContext db) : IUnitOfWork
{
    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return db.SaveChangesAsync(cancellationToken);
    }
}