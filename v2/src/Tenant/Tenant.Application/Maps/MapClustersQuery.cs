using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Maps;

public enum DialectMode
{
    All,
    AiAdmin,
    AdminOnly
}

public record MapClustersQuery(
    MapBounds Bounds,
    bool Clustered,
    double ClusterDistanceMeters,
    MapOwnerScope OwnerScope,
    int? UserId,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    bool OnlyMeaningfulDialects,
    bool HideOthersWithoutMeaningfulDialect,
    DialectMode DialectMode);
