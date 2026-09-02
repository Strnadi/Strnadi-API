using Tenant.Domain.Entities;

namespace Tenant.Domain.Persistence.Repositories;

public interface IDetectedDialectsRepository
{
    Task<DetectedDialect[]> GetAllAsync(CancellationToken cancellationToken = default);
    
    Task<DetectedDialect?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    
    Task<DetectedDialect?> GetByFilteredPartIdAsync(int filteredPartId, CancellationToken cancellationToken = default);

    void Add(DetectedDialect detectedDialect);
    
    void Remove(DetectedDialect detectedDialect);
}