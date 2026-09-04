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
    double Zoom,
    bool Clustered,
    int? ClusterResolutionPx,
    MapOwnerScope OwnerScope,
    int? UserId,
    DateTime? CreatedFromUtc,
    DateTime? CreatedToUtc,
    bool OnlyMeaningfulDialects,
    bool HideOthersWithoutMeaningfulDialect,
    DialectMode DialectMode);
