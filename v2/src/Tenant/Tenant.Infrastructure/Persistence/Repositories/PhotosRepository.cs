using Microsoft.EntityFrameworkCore;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Infrastructure.Persistence.Repositories;

public class PhotosRepository(TenantDbContext db) : IPhotosRepository
{
    public async Task<Photo?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        return await db.Photos.FirstOrDefaultAsync(p => p.UserId == userId, cancellationToken);
    }

    public async Task<Photo?> GetByRecordingIdAsync(int recordingId, CancellationToken cancellationToken = default)
    {
        return await db.Photos.FirstOrDefaultAsync(p => p.RecordingId == recordingId, cancellationToken);
    }

    public void Add(Photo photo) => db.Photos.Add(photo);
}