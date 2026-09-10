using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Maps;

public record Coords(double Latitude, double Longitude);

public record MapClustersResult(
    MapBounds Bounds,
    bool Clustered,
    double? ClusterDistanceMeters,
    int VisibleRecordingCount,
    object[] Features);

public record DialectAggregate(
    int Id,
    string DialectCode,
    string Color,
    int HintOrder,
    bool IsDialect,
    int ContributionCount,
    double Percentage);

public record RecordingFeature(
    int RecordingId,
    int? RepresentativePartId,
    int LocationPartId,
    string LocationSource,
    double Latitude,
    double Longitude,
    string? Name,
    DateTime CreatedAt,
    DialectAggregate[] Dialects,
    string Source)
{
    public string Kind => "recording";
}

public record ClusterItem(
    int RecordingId,
    int? RepresentativePartId,
    int LocationPartId,
    string LocationSource,
    string? Name,
    DateTime CreatedAt,
    Coords Position,
    string Source);

public record ClusterFeature(
    string Id,
    double Latitude,
    double Longitude,
    MapBounds Bounds,
    int Count,
    DialectAggregate[] Dialects,
    string Source,
    ClusterItem[] Items,
    bool HasMoreItems,
    string? NextItemsCursor)
{
    public string Kind => "cluster";
}

public record ClusterItemsPage(string ClusterId, int Count, ClusterItem[] Items, bool HasMoreItems, string? NextItemsCursor);
