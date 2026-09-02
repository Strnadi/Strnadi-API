using Microsoft.EntityFrameworkCore;
using Strnadi.Domain.Entities;
using Strnadi.Domain.Persistence.Repositories;

namespace Strnadi.Infrastructure.Persistence.Repositories;

public class RecordingPartsRepository(AppDbContext db) : IRecordingPartsRepository
{
    public Task<RecordingPart?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.RecordingParts.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    public void Add(RecordingPart recordingPart) => db.RecordingParts.Add(recordingPart);
}
