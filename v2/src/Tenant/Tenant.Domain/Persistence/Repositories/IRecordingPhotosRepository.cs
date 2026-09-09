using Tenant.Domain.Entities;

namespace Tenant.Domain.Persistence.Repositories;

public interface IRecordingPhotosRepository
{
    Task<RecordingPhoto[]> GetAllByRecordingIdAsync(int recordingId, CancellationToken cancellationToken = default);

    Task<RecordingPhoto?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    void Add(RecordingPhoto photo);

    void Remove(RecordingPhoto photo);
}
