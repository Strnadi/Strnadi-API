using Tenant.Domain.Entities;

namespace Tenant.Domain.Persistence.Repositories;

public interface IRecordingPartsRepository
{
    Task<RecordingPart?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    void Add(RecordingPart recordingPart);
}