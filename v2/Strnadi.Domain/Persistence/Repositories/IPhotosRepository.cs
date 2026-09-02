using Strnadi.Domain.Entities;

namespace Strnadi.Domain.Persistence.Repositories;

public interface IPhotosRepository
{
    Task<Photo?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
    
    Task<Photo?> GetByRecordingIdAsync(int recordingId, CancellationToken cancellationToken = default);
    
    void Add(Photo photo);
}