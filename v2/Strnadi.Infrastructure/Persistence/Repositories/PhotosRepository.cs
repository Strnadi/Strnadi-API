using Microsoft.EntityFrameworkCore;
using Strnadi.Domain.Entities;
using Strnadi.Domain.Persistence.Repositories;

namespace Strnadi.Infrastructure.Persistence.Repositories;

public class PhotosRepository(AppDbContext db) : IPhotosRepository
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