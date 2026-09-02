using Tenant.Domain.Entities;

namespace Tenant.Domain.Persistence.Repositories;

public interface IFilteredRecordingPartsRepository
{
    Task<FilteredRecordingPart[]> GetAllAsync(int? recordingId, bool? verified, CancellationToken cancellationToken = default);
    
    Task<FilteredRecordingPart?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    void Add(FilteredRecordingPart filteredPart);
    
    void Remove(FilteredRecordingPart filteredPart);
}