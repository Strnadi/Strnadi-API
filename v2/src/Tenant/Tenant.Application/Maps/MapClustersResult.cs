using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Maps;

public record MapClustersResult(MapBounds Bounds, int ClusterResolutionPx, double DetailZoomThreshold, MapCluster[] Clusters);

public record MapCluster(
    string Id,
    double CenterLatitude,
    double CenterLongitude,
    int Count,
    int RadiusPx,
    bool Expandable,
    bool Leaf,
    MapDialectBreakdown[] Dialects,
    MapClusterItem[]? Items);

public record MapDialectBreakdown(int? DialectId, string Key, string Label, int Count, double Ratio);

public record MapClusterItem(
    int RecordingId,
    int PartId,
    double Latitude,
    double Longitude,
    DateTime CreatedAt,
    int? DialectId,
    string? DialectLabel,
    string Source);
