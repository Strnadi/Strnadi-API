using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Maps;

public enum DialectMode { All, AiAdmin, AdminOnly }

public record MapClustersQuery(
    double? CenterLatitude,
    double? CenterLongitude,
    double Zoom,
    int? ViewportWidthPx,
    int? ViewportHeightPx,
    double DevicePixelRatio,
    MapBounds? Bounds,
    DialectMode DialectMode,
    bool Verified,
    int? UserId,
    DateOnly? CreatedFrom,
    DateOnly? CreatedTo,
    int MaxItemsPerCluster);
