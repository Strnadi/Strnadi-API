using System.Text.Json.Serialization;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Maps;

public record MapClustersResult(
    MapBounds Bounds,
    bool Clustered,
    double? ClusterDistanceMeters,
    int VisibleRecordingCount,
    MapFeature[] Features);

[JsonConverter(typeof(JsonStringEnumConverter<MapDialectSource>))]
public enum MapDialectSource
{
    [JsonStringEnumMemberName("unknown")]
    Unknown,
    [JsonStringEnumMemberName("confirmed")]
    Confirmed,
    [JsonStringEnumMemberName("ai")]
    Ai,
    [JsonStringEnumMemberName("user")]
    User
}

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
    MapDialectSource Source) : MapFeature;

public sealed record MapClusterFeature(
    string Id,
    double Latitude,
    double Longitude,
    MapBounds Bounds,
    int Count,
    string[] DialectCodes,
    MapDialectSource Source,
    MapClusterItem[] Items) : MapFeature;

public record MapClusterItem(
    int RecordingId,
    int RepresentativePartId,
    string? Name,
    DateTime CreatedAt,
    MapPosition Position);

public record MapPosition(double Latitude, double Longitude);
