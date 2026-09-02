using Microsoft.EntityFrameworkCore;
using Strnadi.Domain.Entities;
using Strnadi.Domain.Persistence.Repositories;

namespace Strnadi.Infrastructure.Persistence.Repositories;

public class RecordingsRepository(AppDbContext db) : IRecordingsRepository
{
    public Task<Recording[]> GetAllAsync(int? userId, CancellationToken cancellationToken = default)
    {
        var query = db.Recordings.Where(r => r.Deleted != true);

        if (userId is not null)
            query = query.Where(r => r.UserId == userId);

        return query.ToArrayAsync(cancellationToken);
    }

    public Task<Recording[]> GetAllAsync(int? userId, bool includeParts, CancellationToken cancellationToken = default)
    {
        var query = db.Recordings.Where(r => r.Deleted != true);

        if (userId is not null)
            query = query.Where(r => r.UserId == userId);

        if (includeParts)
            query = query.Include(r => r.RecordingParts);

        return query.ToArrayAsync(cancellationToken);
    }

    public Task<Recording[]> GetDeletedAsync(CancellationToken cancellationToken = default) =>
        db.Recordings.Where(r => r.Deleted == true).ToArrayAsync(cancellationToken);

    public Task<Recording[]> GetIncompleteAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Recordings
            .Where(r => r.UserId == userId && r.Deleted != true)
            .Where(r => r.ExpectedPartsCount != null && r.RecordingParts.Count() < r.ExpectedPartsCount)
            .ToArrayAsync(cancellationToken);

    public Task<Recording?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Recordings.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Recording?> GetByIdAsync(int id, bool includeParts, CancellationToken cancellationToken = default)
    {
        IQueryable<Recording> query = db.Recordings;

        if (includeParts)
            query = query.Include(r => r.RecordingParts);

        return query.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default) =>
        db.Recordings.AnyAsync(r => r.Id == id, cancellationToken);

    public void Add(Recording recording) => db.Recordings.Add(recording);

    public void Remove(Recording recording) => db.Recordings.Remove(recording);
}
