using Microsoft.EntityFrameworkCore;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Infrastructure.Persistence.Repositories;

public class DialectsRepository(AppDbContext db) : IDialectsRepository
{
    public Task<Dialect[]> GetAllAsync(CancellationToken cancellationToken = default) =>
        db.Dialects.ToArrayAsync(cancellationToken);

    public Task<Dialect?> GetByCodeAsync(string code, CancellationToken cancellationToken = default) =>
        db.Dialects.FirstOrDefaultAsync(d => d.DialectCode == code, cancellationToken);
}
