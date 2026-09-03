using Microsoft.EntityFrameworkCore;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Infrastructure.Persistence.Repositories;

public class DetectedDialectsRepository(TenantDbContext db) : IDetectedDialectsRepository
{
    public Task<DetectedDialect[]> GetAllAsync(CancellationToken cancellationToken = default) =>
        db.DetectedDialects.ToArrayAsync(cancellationToken);

    public Task<DetectedDialect?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.DetectedDialects.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    public Task<DetectedDialect?> GetByFilteredPartIdAsync(int filteredPartId, CancellationToken cancellationToken = default) =>
        db.DetectedDialects.FirstOrDefaultAsync(d => d.FilteredRecordingPartId == filteredPartId, cancellationToken);

    public void Add(DetectedDialect detectedDialect) => db.DetectedDialects.Add(detectedDialect);

    public void Remove(DetectedDialect detectedDialect) => db.DetectedDialects.Remove(detectedDialect);
}
