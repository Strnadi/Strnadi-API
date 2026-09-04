namespace Tenant.Domain.Persistence.Repositories;

public interface IMapPointsRepository
{
    Task<MapRecordingCandidate[]> GetInBoundsAsync(
        MapBounds bounds,
        MapPointFilters filters,
        CancellationToken cancellationToken = default);
}
