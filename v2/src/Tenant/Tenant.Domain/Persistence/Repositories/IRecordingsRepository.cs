using Tenant.Domain.Entities;

namespace Tenant.Domain.Persistence.Repositories;

public interface IRecordingsRepository
{
    Task<Recording[]> GetAllAsync(Guid? userId, CancellationToken cancellationToken = default);

    Task<Recording[]> GetAllAsync(Guid? userId, bool includeParts, CancellationToken cancellationToken = default);

    Task<Recording[]> GetDeletedAsync(CancellationToken cancellationToken = default);

    Task<Recording[]> GetIncompleteAsync(Guid userId, CancellationToken cancellationToken = default);

    Task<Recording?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Recording?> GetByIdAsync(int id, bool includeParts, CancellationToken cancellationToken = default);
    
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    void Add(Recording recording);

    void Remove(Recording recording);
}