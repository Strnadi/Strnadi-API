using Tenant.Domain.Entities;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Maps;

public class CatalogInconsistentException(string message) : Exception(message);

public class MapClustersService(IMapPointsRepository mapPoints, IDialectsRepository dialects, ClusterSnapshotStore snapshotStore)
{
    private const double TargetGridPx = 48;
    private const double MaxProjectionLatitude = 85.05112878;
    private const double EarthCircumferenceMeters = 40075016.68557849;
    private const int PreviewItemLimit = 5;

    public async Task<MapClustersResult> GetClustersAsync(MapClustersQuery query, CancellationToken cancellationToken = default)
    {
        var bounds = query.Bounds ?? ResolveBoundsFromCenter(
            query.Center!.Latitude, query.Center!.Longitude, query.Zoom!.Value,
            query.ViewportWidthPx!.Value, query.ViewportHeightPx!.Value, query.DevicePixelRatio);

        var catalog = await dialects.GetAllAsync(cancellationToken);
        var dialectsById = catalog.ToDictionary(d => d.Id);
        var unknown = catalog.FirstOrDefault(d => d.DialectCode == "Unknown")
            ?? throw new CatalogInconsistentException("Dialect catalog is missing the 'unknown' row required for map-clusters fallbacks.");

        var filters = new MapRecordingFilters(query.OwnerScope, query.UserId, query.CreatedFrom, query.CreatedTo);
        var candidates = await mapPoints.GetCandidatesInBoundsAsync(bounds, filters, cancellationToken);

        var resolved = candidates
            .Select(c => RecordingPointResolver.Resolve(c, query.DialectMode, unknown.Id))
            .Where(p => p is not null)
            .Select(p => p!)
            .Where(p => IsInBounds(p.Latitude, p.Longitude, bounds))
            .ToList();

        bool HasMeaningfulDialect(ResolvedRecordingPoint p) =>
            p.Votes.Any(v => dialectsById.TryGetValue(v.DialectId, out var d) && d.IsDialect);

        if (query.OnlyMeaningfulDialects)
            resolved = resolved.Where(HasMeaningfulDialect).ToList();

        if (query.HideOthersWithoutMeaningfulDialect)
        {
            resolved = resolved
                .Where(p => HasMeaningfulDialect(p) || (query.UserId is not null && p.UserId == query.UserId))
                .ToList();
        }

        int visibleRecordingCount = resolved.Count;

        if (!query.Clustered)
        {
            var features = resolved
                .Select(p => (object)BuildRecordingFeature(p, dialectsById))
                .ToArray();

            return new MapClustersResult(bounds, false, null, visibleRecordingCount, features);
        }

        double refLatitude = Math.Clamp((bounds.North + bounds.South) / 2, -MaxProjectionLatitude, MaxProjectionLatitude);
        double gridZoom = query.Zoom ?? 0;
        double metersPerLogicalPixel = MetersPerLogicalPixel(refLatitude, gridZoom);
        double clusterDistanceMeters = query.ClusterDistanceMetersOverride ?? TargetGridPx * metersPerLogicalPixel;
        double gridCellPx = clusterDistanceMeters / metersPerLogicalPixel;

        var groups = GroupIntoCells(resolved, gridZoom, gridCellPx, bounds, query.MixDialects, query.MixSources);

        var builtFeatures = groups
            .Select(g => BuildFeature(g, dialectsById))
            .ToArray();

        return new MapClustersResult(bounds, true, clusterDistanceMeters, visibleRecordingCount, builtFeatures);
    }

    public ClusterItemsPage? GetClusterItemsPage(string clusterId, string cursor, int pageSize)
    {
        if (!ClusterSnapshotStore.TryDecodeCursor(cursor, out var decoded) || decoded.ClusterId != clusterId)
            return null;

        var entry = snapshotStore.TryGet(clusterId);
        if (entry is null || entry.Token != decoded.Token)
            return null;

        if (decoded.Offset < 0 || decoded.Offset > entry.ItemsBeyondPreview.Length)
            return null;

        var page = entry.ItemsBeyondPreview.Skip(decoded.Offset).Take(pageSize).ToArray();
        bool hasMore = decoded.Offset + page.Length < entry.ItemsBeyondPreview.Length;
        string? nextCursor = hasMore ? snapshotStore.EncodeNextCursor(clusterId, entry.Token, decoded.Offset + page.Length) : null;

        return new ClusterItemsPage(clusterId, entry.MemberRecordingIds.Length, page, hasMore, nextCursor);
    }

    public async Task<bool> ClusterMembersStillVisibleAsync(string clusterId, CancellationToken cancellationToken = default)
    {
        var entry = snapshotStore.TryGet(clusterId);
        if (entry is null)
            return false;

        var visible = await mapPoints.GetVisibleRecordingIdsAsync(entry.MemberRecordingIds, cancellationToken);
        return visible.Length == entry.MemberRecordingIds.Length;
    }

    private object BuildFeature(ResolvedRecordingPoint[] members, IReadOnlyDictionary<int, Dialect> dialectsById)
    {
        if (members.Length == 1)
            return BuildRecordingFeature(members[0], dialectsById);

        double latitude = members.Average(m => m.Latitude);
        double longitude = CircularMeanLongitude(members.Select(m => m.Longitude));

        double north = members.Max(m => m.Latitude);
        double south = members.Min(m => m.Latitude);
        var lonBounds = AntimeridianAwareLonBounds(members.Select(m => m.Longitude));

        var allVotes = members.SelectMany(m => m.Votes).ToArray();
        var dialectAggregates = AggregateDialects(allVotes, dialectsById);

        var distinctSources = members.Select(m => m.Source).Distinct().ToArray();
        string source = distinctSources.Length == 1 ? ToWireString(distinctSources[0]) : "mixed";

        var orderedMembers = members.OrderBy(m => m.RecordingId).ToArray();
        var allItems = orderedMembers.Select(ToClusterItem).ToArray();
        var previewItems = allItems.Take(PreviewItemLimit).ToArray();
        var beyondPreview = allItems.Skip(PreviewItemLimit).ToArray();

        string clusterId = BuildClusterId(members);
        bool hasMoreItems = beyondPreview.Length > 0;
        string? nextCursor = hasMoreItems
            ? snapshotStore.StoreAndGetFirstCursor(clusterId, orderedMembers.Select(m => m.RecordingId).ToArray(), beyondPreview)
            : null;

        return new ClusterFeature(
            clusterId,
            latitude,
            longitude,
            new MapBounds(north, south, lonBounds.East, lonBounds.West),
            members.Length,
            dialectAggregates,
            source,
            previewItems,
            hasMoreItems,
            nextCursor);
    }

    private static RecordingFeature BuildRecordingFeature(ResolvedRecordingPoint point, IReadOnlyDictionary<int, Dialect> dialectsById)
    {
        return new RecordingFeature(
            point.RecordingId,
            point.RepresentativePartId,
            point.LocationPartId,
            point.LocationSource == LocationSource.Representative ? "representative" : "latestPart",
            point.Latitude,
            point.Longitude,
            point.Name,
            point.CreatedAt,
            AggregateDialects(point.Votes, dialectsById),
            ToWireString(point.Source));
    }

    private static ClusterItem ToClusterItem(ResolvedRecordingPoint point) => new(
        point.RecordingId,
        point.RepresentativePartId,
        point.LocationPartId,
        point.LocationSource == LocationSource.Representative ? "representative" : "latestPart",
        point.Name,
        point.CreatedAt,
        new Coords(point.Latitude, point.Longitude),
        ToWireString(point.Source));

    internal static string ToWireString(ContributionSource source) => source switch
    {
        ContributionSource.Confirmed => "confirmed",
        ContributionSource.Ai => "ai",
        ContributionSource.User => "user",
        _ => "unknown"
    };

    internal static DialectAggregate[] AggregateDialects(DialectVote[] votes, IReadOnlyDictionary<int, Dialect> dialectsById)
    {
        var counted = votes
            .GroupBy(v => v.DialectId)
            .Select(g => (Dialect: dialectsById.TryGetValue(g.Key, out var d)
                    ? d
                    : throw new CatalogInconsistentException($"Dialect catalog is missing id {g.Key} referenced by a resolved vote."),
                Count: g.Count()))
            // Pre-sort by (DialectCode, Id) so the largest-remainder tie-break below - a stable
            // sort preserving this order on equal fractions - matches the CR's tie rule.
            .OrderBy(x => x.Dialect.DialectCode, StringComparer.Ordinal)
            .ThenBy(x => x.Dialect.Id)
            .ToArray();

        var percentages = LargestRemainderPercentages(counted.Select(x => x.Count).ToArray());

        return counted
            .Select((x, i) => new DialectAggregate(x.Dialect.Id, x.Dialect.DialectCode, x.Dialect.Color, x.Dialect.HintOrder, x.Dialect.IsDialect, x.Count, percentages[i]))
            .OrderBy(a => a.HintOrder)
            .ThenBy(a => a.Id)
            .ToArray();
    }

    // Largest-remainder rounding to 2 decimals, summing to exactly 100.00. Input order matters for
    // tie-breaking (equal fractional remainders keep the input's relative order via a stable sort).
    internal static double[] LargestRemainderPercentages(int[] counts)
    {
        int total = counts.Sum();
        if (total == 0)
            return new double[counts.Length];

        var basisPoints = counts.Select(c => c * 10000.0 / total).ToArray();
        var floors = basisPoints.Select(b => (int)Math.Floor(b)).ToArray();
        int remainder = 10000 - floors.Sum();

        var byRemainderDesc = Enumerable.Range(0, counts.Length)
            .OrderByDescending(i => basisPoints[i] - floors[i])
            .ToArray();

        for (int k = 0; k < remainder; k++)
            floors[byRemainderDesc[k]] += 1;

        return floors.Select(f => f / 100.0).ToArray();
    }

    private static string BuildClusterId(ResolvedRecordingPoint[] members)
    {
        // Deterministic for identical membership across requests, opaque to the client per the CR.
        unchecked
        {
            int hash = 17;
            foreach (var id in members.Select(m => m.RecordingId).OrderBy(id => id))
                hash = hash * 31 + id;
            return $"c-{(uint)hash:x8}";
        }
    }

    private static double CircularMeanLongitude(IEnumerable<double> longitudes)
    {
        double x = 0, y = 0;
        int n = 0;
        foreach (var lng in longitudes)
        {
            double rad = lng * Math.PI / 180d;
            x += Math.Cos(rad);
            y += Math.Sin(rad);
            n++;
        }

        if (n == 0) return 0;
        return Math.Atan2(y / n, x / n) * 180d / Math.PI;
    }

    private static (double West, double East) AntimeridianAwareLonBounds(IEnumerable<double> longitudes)
    {
        var lngs = longitudes.ToArray();
        double west = lngs.Min();
        double east = lngs.Max();

        // If the naive span is implausibly wide (>180deg), the group likely straddles the
        // antimeridian; recompute using longitudes shifted into [0,360) so they become contiguous.
        if (east - west > 180)
        {
            var shifted = lngs.Select(l => l < 0 ? l + 360 : l).ToArray();
            double shiftedWest = shifted.Min();
            double shiftedEast = shifted.Max();
            west = shiftedWest > 180 ? shiftedWest - 360 : shiftedWest;
            east = shiftedEast > 180 ? shiftedEast - 360 : shiftedEast;
        }

        return (west, east);
    }

    internal static IEnumerable<ResolvedRecordingPoint[]> GroupIntoCells(
        List<ResolvedRecordingPoint> points, double zoom, double cellPx, MapBounds bounds, bool mixDialects, bool mixSources)
    {
        bool crossesAntimeridian = bounds.West > bounds.East;

        return points
            .GroupBy(p =>
            {
                double lng = crossesAntimeridian && p.Longitude < bounds.West ? p.Longitude + 360 : p.Longitude;
                var (x, y) = ProjectToPixels(p.Latitude, lng, zoom);
                long cellX = (long)Math.Floor(x / cellPx);
                long cellY = (long)Math.Floor(y / cellPx);

                string dialectSetKey = mixDialects
                    ? ""
                    : string.Join(",", p.Votes.Select(v => v.DialectId).Distinct().OrderBy(id => id));
                string sourceKey = mixSources ? "" : p.Source.ToString();

                return (cellX, cellY, dialectSetKey, sourceKey);
            })
            .Select(g => g.ToArray());
    }

    internal static bool IsInBounds(double lat, double lng, MapBounds bounds)
    {
        if (lat < bounds.South || lat > bounds.North)
            return false;

        return bounds.West <= bounds.East
            ? lng >= bounds.West && lng <= bounds.East
            : lng >= bounds.West || lng <= bounds.East;
    }

    private static MapBounds ResolveBoundsFromCenter(double centerLat, double centerLng, double zoom, int viewportWidthPx, int viewportHeightPx, double devicePixelRatio)
    {
        var (centerX, centerY) = ProjectToPixels(centerLat, centerLng, zoom);

        double halfWidth = viewportWidthPx / 2d;
        double halfHeight = viewportHeightPx / 2d;

        var (north, west) = UnprojectFromPixels(centerX - halfWidth, centerY - halfHeight, zoom);
        var (south, east) = UnprojectFromPixels(centerX + halfWidth, centerY + halfHeight, zoom);

        north = Math.Clamp(north, -MaxProjectionLatitude, MaxProjectionLatitude);
        south = Math.Clamp(south, -MaxProjectionLatitude, MaxProjectionLatitude);

        return new MapBounds(north, south, east, west);
    }

    private static double MetersPerLogicalPixel(double latitude, double zoom) =>
        EarthCircumferenceMeters * Math.Cos(latitude * Math.PI / 180d) / (256d * Math.Pow(2d, zoom));

    // Reverse Mercator
    private static (double Lat, double Lng) UnprojectFromPixels(double x, double y, double zoom)
    {
        var scale = 256d * Math.Pow(2d, zoom);

        double lng = x / scale * 360 - 180;

        double n = Math.PI - 2 * Math.PI * y / scale;
        double lat = 180 / Math.PI * Math.Atan(Math.Sinh(n));

        return (lat, lng);
    }

    // Mercator projection
    private static (double X, double Y) ProjectToPixels(double lat, double lng, double zoom)
    {
        double scale = 256d * Math.Pow(2d, zoom);

        double x = (lng + 180d) / 360d * scale;

        double sinLat = Math.Sin(lat * Math.PI / 180d);
        double y = (0.5 - Math.Log((1 + sinLat) / (1 - sinLat)) / (4 * Math.PI)) * scale;

        return (x, y);
    }
}
