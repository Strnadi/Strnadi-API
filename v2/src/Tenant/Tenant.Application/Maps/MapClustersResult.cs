using System.Text.Json.Serialization;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Maps;

public record MapClustersResult(
    MapBounds Bounds,
    double Zoom,
    bool Clustered,
    int? ClusterResolutionPx,
    double DetailZoomThreshold,
    int VisibleRecordingCount,
    MapFeature[] Features);

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(MapRecordingFeature), "recording")]
[JsonDerivedType(typeof(MapClusterFeature), "cluster")]
public abstract record MapFeature;

public sealed record MapRecordingFeature(
    int RecordingId,
    int RepresentativePartId,
    double Latitude,
    double Longitude,
    string? Name,
    DateTime CreatedAt,
    string[] DialectCodes,
    string Source) : MapFeature;

public sealed record MapClusterFeature(
    string Id,
    double Latitude,
    double Longitude,
    MapBounds Bounds,
    int Count,
    double ExpansionZoom,
    string[] DialectCodes,
    string Source,
    MapClusterItem[]? Items) : MapFeature;

public record MapClusterItem(
    int RecordingId,
    int RepresentativePartId,
    string? Name,
    DateTime CreatedAt,
    MapPosition Position);

public record MapPosition(double Latitude, double Longitude);
