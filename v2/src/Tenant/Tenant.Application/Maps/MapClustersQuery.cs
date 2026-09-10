using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Maps;

public enum DialectMode { All, AiAdmin, AdminOnly }

public record MapClustersQuery(
    Coords? Center,
    double? Zoom,
    int? ViewportWidthPx,
    int? ViewportHeightPx,
    double DevicePixelRatio,
    MapBounds? Bounds,
    bool Clustered,
    bool MixDialects,
    bool MixSources,
    double? ClusterDistanceMetersOverride,
    OwnerScope OwnerScope,
    Guid? UserId,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo,
    bool OnlyMeaningfulDialects,
    bool HideOthersWithoutMeaningfulDialect,
    DialectMode DialectMode);
