using Microsoft.EntityFrameworkCore;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Infrastructure.Persistence.Repositories;

public class RecordingPartsRepository(TenantDbContext db) : IRecordingPartsRepository
{
    public Task<RecordingPart?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.RecordingParts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Add(RecordingPart recordingPart) => db.RecordingParts.Add(recordingPart);
}
