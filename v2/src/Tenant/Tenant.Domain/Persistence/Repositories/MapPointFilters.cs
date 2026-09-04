namespace Tenant.Domain.Persistence.Repositories;

public enum MapOwnerScope
{
    All,
    Mine,
    Others
}

public record MapPointFilters(
    MapOwnerScope OwnerScope,
    int? UserId,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc);
