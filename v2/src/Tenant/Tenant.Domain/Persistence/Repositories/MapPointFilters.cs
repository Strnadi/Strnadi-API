namespace Tenant.Domain.Persistence.Repositories;

// Note: no DialectMode here on purpose. The repository always returns all three dialect id tiers
// (confirmed/predicted/user-guess) for a point; picking which one is "the" dialect per dialectMode
// is a display concern, not a row-filtering concern, so it belongs to Application (MapClustersService),
// not this query.
public record MapPointFilters(bool Verified, Guid? UserId, DateOnly? CreatedFrom, DateOnly? CreatedTo);
