namespace Tenant.Domain.Persistence.Repositories;

public interface IMapPointsRepository
{
    Task<MapPointCandidate[]> GetInBoundsAsync(MapBounds bounds, MapPointFilters filters, CancellationToken cancellationToken = default);
}
