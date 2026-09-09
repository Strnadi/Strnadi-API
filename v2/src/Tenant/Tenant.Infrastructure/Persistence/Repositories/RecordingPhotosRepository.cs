using Microsoft.EntityFrameworkCore;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Infrastructure.Persistence.Repositories;

public class RecordingPhotosRepository(TenantDbContext db) : IRecordingPhotosRepository
{
    public Task<RecordingPhoto[]> GetAllByRecordingIdAsync(int recordingId, CancellationToken cancellationToken = default) =>
        db.RecordingPhotos.Where(p => p.RecordingId == recordingId).ToArrayAsync(cancellationToken);

    public Task<RecordingPhoto?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.RecordingPhotos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Add(RecordingPhoto photo) => db.RecordingPhotos.Add(photo);

    public void Remove(RecordingPhoto photo) => db.RecordingPhotos.Remove(photo);
}
