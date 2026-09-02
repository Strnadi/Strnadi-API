using Strnadi.Domain.Entities;

namespace Strnadi.Domain.Persistence.Repositories;

public interface IRecordingPartsRepository
{
    Task<RecordingPart?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    void Add(RecordingPart recordingPart);
}