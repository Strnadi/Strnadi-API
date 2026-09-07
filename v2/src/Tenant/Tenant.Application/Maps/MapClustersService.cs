using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Maps;

public class MapClustersService(IMapPointsRepository mapPoints, IDialectsRepository dialects)
{
    private const int ClusterResolutionPx = 48;
    private const double DetailZoomThreshold = 16;

    public async Task<MapClustersResult> GetClustersAsync(MapClustersQuery query, CancellationToken cancellationToken = default)
    {
        var bounds = query.Bounds ?? ResolveBounds(
            query.Center!.Latitude, query.Center!.Longitude, query.Zoom,
            query.ViewportWidthPx!.Value, query.ViewportHeightPx!.Value);

        var filters = new MapPointFilters(query.Verified, query.UserId, query.CreatedFrom, query.CreatedTo);
        var points = await mapPoints.GetInBoundsAsync(bounds, filters, cancellationToken);

        var allDialects = await dialects.GetAllAsync(cancellationToken);
        var dialectCodesById = allDialects.ToDictionary(d => d.Id, d => d.DialectCode);
        var dialectColorsById = allDialects.ToDictionary(d => d.Id, d => d.Color);

        var clusters = GroupIntoCells(points, query.Zoom, ClusterResolutionPx)
            .Select(group => BuildCluster(group.Key, group.ToArray(), dialectCodesById, dialectColorsById, query.Zoom, query.DialectMode, query.MaxItemsPerCluster))
            .ToArray();

        return new MapClustersResult(bounds, ClusterResolutionPx, DetailZoomThreshold, clusters);
    }

    private static MapCluster BuildCluster(
        (long CellX, long CellY) cellKey,
        MapPointCandidate[] points,
        IReadOnlyDictionary<int, string> dialectCodesById,
        IReadOnlyDictionary<int, string> dialectColorsById,
        double zoom,
        DialectMode dialectMode,
        int maxItemsPerCluster)
    {
        int count = points.Length;

        var center = new Coords(points.Average(p => p.Latitude), points.Average(p => p.Longitude));

        var dialectBreakdown = points
            .Select(p => ResolveDialectId(p, dialectMode))
            .GroupBy(dialectId => dialectId)
            .Select(g =>
            {
                string label = g.Key is not null && dialectCodesById.TryGetValue(g.Key.Value, out var code) ? code : "unknown";
                string color = g.Key is not null && dialectColorsById.TryGetValue(g.Key.Value, out var hex) ? hex : "#808080";
                return new MapDialectBreakdown(g.Key, color, label, g.Count(), (double)g.Count() / count);
            })
            .ToArray();

        // Placeholder scaling - server-side min/max as required by the CR, exact curve can be tuned later.
        int radiusPx = Math.Clamp(12 + (int)(Math.Sqrt(count) * 6), 12, 60);

        bool leaf = zoom >= DetailZoomThreshold && count <= maxItemsPerCluster;

        string id = $"z{zoom:0.#}_{cellKey.CellX}_{cellKey.CellY}";

        MapClusterItem[]? items = leaf
            ? points.Take(maxItemsPerCluster).Select(p =>
            {
                var dialectId = ResolveDialectId(p, dialectMode);
                string? label = dialectId is not null && dialectCodesById.TryGetValue(dialectId.Value, out var code) ? code : null;
                return new MapClusterItem(p.RecordingId, p.PartId, new Coords(p.Latitude, p.Longitude), p.CreatedAt,
                    dialectId, label, ResolveDialectSource(p, dialectMode));
            }).ToArray()
            : null;

        return new MapCluster(id, center, count, radiusPx, Expandable: !leaf, leaf, dialectBreakdown, items);
    }

    private static string ResolveDialectSource(MapPointCandidate point, DialectMode dialectMode) => dialectMode switch
    {
        DialectMode.AdminOnly => point.ConfirmedDialectId is not null ? "admin" : "unknown",
        DialectMode.AiAdmin => point.ConfirmedDialectId is not null ? "admin" : point.PredictedDialectId is not null ? "ai" : "unknown",
        DialectMode.All => point.ConfirmedDialectId is not null ? "admin"
            : point.PredictedDialectId is not null ? "ai"
            : point.UserGuessDialectId is not null ? "user" : "unknown",
        _ => "unknown"
    };

    private static MapBounds ResolveBounds(
        double centerLat, double centerLng, double zoom, int viewportWidthPx, int viewportHeightPx)
    {
        var (centerX, centerY) = ProjectToPixels(centerLat, centerLng, zoom);
        
        double left = centerX - viewportWidthPx;
        double right = centerX + viewportWidthPx;
        double top = centerY - viewportHeightPx;
        double bottom = centerY + viewportHeightPx;
        
        var (north, west) = UnprojectFromPixels(left, top, zoom);
        var (south, east) = UnprojectFromPixels(right, bottom, zoom);

        return new MapBounds(north, south, east, west);
    }

    private static int? ResolveDialectId(MapPointCandidate point, DialectMode dialectMode)
    {
        return dialectMode switch
        {
            DialectMode.All => point.ConfirmedDialectId ?? point.PredictedDialectId ?? point.UserGuessDialectId,
            DialectMode.AiAdmin => point.ConfirmedDialectId ?? point.PredictedDialectId,
            DialectMode.AdminOnly => point.ConfirmedDialectId
        };
    }

    private static IEnumerable<IGrouping<(long CellX, long CellY), MapPointCandidate>> GroupIntoCells(
        MapPointCandidate[] points, double zoom, int clusterResolutionPx)
    {
        return points.GroupBy(p =>
        {
            var (x, y) = ProjectToPixels(p.Latitude, p.Longitude, zoom);
            return ((long)(x / clusterResolutionPx), (long)(y / clusterResolutionPx));
        });
    }

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
