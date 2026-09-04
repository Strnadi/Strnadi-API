using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Maps;

public class MapClustersService(
    IMapPointsRepository mapPoints,
    IDialectsRepository dialects,
    ILogger<MapClustersService> logger)
{
    public const double DetailZoomThreshold = 16;

    private const double MaxMercatorLatitude = 85.05112878;

    private static readonly HashSet<string> NonMeaningfulCodes =
        new(StringComparer.Ordinal)
        {
            "none",
            "nobird",
            "no-bird",
            "unfinished",
            "unknown"
        };

    private static readonly HashSet<string> OmittedConfirmedCodes =
        new(StringComparer.Ordinal)
        {
            "none",
            "nobird",
            "no-bird"
        };

    public async Task<MapClustersResult> GetClustersAsync(
        MapClustersQuery query,
        CancellationToken cancellationToken = default)
    {
        var filters = new MapPointFilters(
            query.OwnerScope,
            query.UserId,
            query.CreatedFromUtc,
            query.CreatedToUtc);

        var candidates = await mapPoints.GetInBoundsAsync(query.Bounds, filters, cancellationToken);
        var allDialects = await dialects.GetAllAsync(cancellationToken);
        var dialectsById = allDialects.ToDictionary(
            dialect => dialect.Id,
            dialect => new DialectInfo(dialect.Id, dialect.DialectCode, dialect.HintOrder));

        var noneDialect = allDialects
            .Where(dialect => NormalizeCode(dialect.DialectCode) == "none")
            .OrderBy(dialect => dialect.HintOrder)
            .ThenBy(dialect => dialect.Id)
            .Select(dialect => new DialectInfo(dialect.Id, dialect.DialectCode, dialect.HintOrder))
            .FirstOrDefault();

        var visible = new List<ProjectedRecording>(candidates.Length);

        foreach (var candidate in candidates)
        {
            var dialectProjection = ResolveDialects(candidate, query.DialectMode, dialectsById, noneDialect);

            if (dialectProjection.WinningTier == DialectTier.Confirmed &&
                dialectProjection.Codes.All(code => OmittedConfirmedCodes.Contains(NormalizeCode(code))))
            {
                continue;
            }

            if (query.OnlyMeaningfulDialects && !dialectProjection.IsMeaningful)
                continue;

            var isReferenceOwner = query.UserId is not null && candidate.UserId == query.UserId;
            if (query.HideOthersWithoutMeaningfulDialect && !isReferenceOwner && !dialectProjection.IsMeaningful)
                continue;

            visible.Add(new ProjectedRecording(
                candidate,
                dialectProjection.Codes,
                dialectProjection.Source));
        }

        MapFeature[] features;
        if (!query.Clustered)
        {
            features = visible
                .OrderBy(point => SourceRank(point.Source))
                .ThenByDescending(point => point.Candidate.CreatedAt)
                .ThenBy(point => point.Candidate.RecordingId)
                .Select(ToRecordingFeature)
                .Cast<MapFeature>()
                .ToArray();
        }
        else
        {
            features = BuildClusteredFeatures(visible, query.Zoom, query.ClusterResolutionPx!.Value);
        }

        return new MapClustersResult(
            query.Bounds,
            query.Zoom,
            query.Clustered,
            query.Clustered ? query.ClusterResolutionPx : null,
            DetailZoomThreshold,
            visible.Count,
            features);
    }

    private static MapFeature[] BuildClusteredFeatures(
        IReadOnlyCollection<ProjectedRecording> recordings,
        double zoom,
        int resolutionPx)
    {
        return recordings
            .GroupBy(recording =>
            {
                var (x, y) = ProjectToWorldPixels(
                    recording.Candidate.Latitude,
                    recording.Candidate.Longitude,
                    zoom);

                return new CellGroupKey(
                    (long)Math.Floor(x / resolutionPx),
                    (long)Math.Floor(y / resolutionPx),
                    recording.Source,
                    string.Join('\u001f', recording.DialectCodes));
            })
            .OrderBy(group => group.Key.CellY)
            .ThenBy(group => group.Key.CellX)
            .ThenBy(group => SourceRank(group.Key.Source))
            .ThenBy(group => group.Key.DialectSignature, StringComparer.Ordinal)
            .Select(group => BuildFeature(group.Key, group.ToArray(), zoom, resolutionPx))
            .ToArray();
    }

    private static MapFeature BuildFeature(
        CellGroupKey key,
        ProjectedRecording[] recordings,
        double zoom,
        int resolutionPx)
    {
        if (recordings.Length == 1)
            return ToRecordingFeature(recordings[0]);

        var projected = recordings
            .Select(recording => ProjectToWorldPixels(
                recording.Candidate.Latitude,
                recording.Candidate.Longitude,
                zoom))
            .ToArray();

        var (latitude, longitude) = UnprojectFromWorldPixels(
            projected.Average(point => point.X),
            projected.Average(point => point.Y),
            zoom);

        var orderedItems = recordings
            .OrderBy(recording => SourceRank(recording.Source))
            .ThenByDescending(recording => recording.Candidate.CreatedAt)
            .ThenBy(recording => recording.Candidate.RecordingId)
            .ToArray();

        var clusterBounds = new MapBounds(
            orderedItems.Max(recording => recording.Candidate.Latitude),
            orderedItems.Min(recording => recording.Candidate.Latitude),
            orderedItems.Max(recording => recording.Candidate.Longitude),
            orderedItems.Min(recording => recording.Candidate.Longitude));

        var items = zoom >= DetailZoomThreshold
            ? orderedItems.Select(ToClusterItem).ToArray()
            : null;

        return new MapClusterFeature(
            CreateClusterId(key, recordings, zoom, resolutionPx),
            latitude,
            longitude,
            clusterBounds,
            recordings.Length,
            FindExpansionZoom(recordings, zoom, resolutionPx),
            recordings[0].DialectCodes,
            recordings[0].Source,
            items);
    }

    private static MapRecordingFeature ToRecordingFeature(ProjectedRecording recording)
    {
        var candidate = recording.Candidate;
        return new MapRecordingFeature(
            candidate.RecordingId,
            candidate.RepresentativePartId,
            candidate.Latitude,
            candidate.Longitude,
            candidate.Name,
            candidate.CreatedAt,
            recording.DialectCodes,
            recording.Source);
    }

    private static MapClusterItem ToClusterItem(ProjectedRecording recording)
    {
        var candidate = recording.Candidate;
        return new MapClusterItem(
            candidate.RecordingId,
            candidate.RepresentativePartId,
            candidate.Name,
            candidate.CreatedAt,
            new MapPosition(candidate.Latitude, candidate.Longitude));
    }

    private static DialectProjection ResolveDialects(
        MapRecordingCandidate candidate,
        DialectMode mode,
        IReadOnlyDictionary<int, DialectInfo> dialectsById,
        DialectInfo? noneDialect)
    {
        var relevantParts = candidate.FilteredParts
            .Where(filtered => candidate.PartRanges.Any(part =>
                filtered.EndDate >= part.StartDate && filtered.StartDate <= part.EndDate))
            .ToArray();

        var confirmed = SelectTier(
            relevantParts.Select(part => part.ConfirmedDialectId),
            DialectTier.Confirmed,
            "confirmed",
            dialectsById);
        if (confirmed is not null)
            return confirmed;

        var representativeParts = relevantParts.Any(part => part.RepresentantFlag == true)
            ? relevantParts.Where(part => part.RepresentantFlag == true).ToArray()
            : relevantParts;

        if (mode is DialectMode.All or DialectMode.AiAdmin)
        {
            var predicted = SelectTier(
                representativeParts.Select(part => part.PredictedDialectId),
                DialectTier.Ai,
                "ai",
                dialectsById);
            if (predicted is not null)
                return predicted;
        }

        if (mode == DialectMode.All)
        {
            var user = SelectTier(
                representativeParts.Select(part => part.UserGuessDialectId),
                DialectTier.User,
                "user",
                dialectsById);
            if (user is not null)
                return user;
        }

        return new DialectProjection(
            [noneDialect?.Code ?? "None"],
            "unknown",
            IsMeaningful: false,
            DialectTier.None);
    }

    private static DialectProjection? SelectTier(
        IEnumerable<int?> dialectIds,
        DialectTier tier,
        string source,
        IReadOnlyDictionary<int, DialectInfo> dialectsById)
    {
        var dialects = dialectIds
            .Where(id => id is not null && dialectsById.ContainsKey(id.Value))
            .Select(id => dialectsById[id!.Value])
            .DistinctBy(dialect => dialect.Id)
            .ToArray();

        if (dialects.Length == 0)
            return null;

        var meaningful = dialects
            .Where(dialect => !NonMeaningfulCodes.Contains(NormalizeCode(dialect.Code)))
            .ToArray();

        var selected = meaningful.Length > 0 ? meaningful : dialects;
        var codes = selected
            .OrderBy(dialect => dialect.HintOrder)
            .ThenBy(dialect => dialect.Id)
            .Select(dialect => dialect.Code)
            .ToArray();

        return new DialectProjection(
            codes,
            meaningful.Length > 0 ? source : "unknown",
            meaningful.Length > 0,
            tier);
    }

    private static string CreateClusterId(
        CellGroupKey key,
        IEnumerable<ProjectedRecording> recordings,
        double zoom,
        int resolutionPx)
    {
        var recordingIds = recordings
            .Select(recording => recording.Candidate.RecordingId)
            .OrderBy(id => id);
        var identity = FormattableString.Invariant(
            $"{zoom:R}|{resolutionPx}|{key.CellX}|{key.CellY}|{key.Source}|{key.DialectSignature}|{string.Join(",", recordingIds)}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return $"map_{Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant()}";
    }

    private static double FindExpansionZoom(
        ProjectedRecording[] recordings,
        double currentZoom,
        int resolutionPx)
    {
        var firstZoom = Math.Max((int)Math.Floor(currentZoom) + 1, 1);
        var lastZoom = (int)DetailZoomThreshold;

        for (var zoom = firstZoom; zoom <= lastZoom; zoom++)
        {
            var cellCount = recordings
                .Select(recording =>
                {
                    var (x, y) = ProjectToWorldPixels(
                        recording.Candidate.Latitude,
                        recording.Candidate.Longitude,
                        zoom);
                    return ((long)Math.Floor(x / resolutionPx), (long)Math.Floor(y / resolutionPx));
                })
                .Distinct()
                .Take(2)
                .Count();

            if (cellCount > 1)
                return zoom;
        }

        return DetailZoomThreshold;
    }

    private static (double X, double Y) ProjectToWorldPixels(double latitude, double longitude, double zoom)
    {
        var scale = 256d * Math.Pow(2d, zoom);
        var clampedLatitude = Math.Clamp(latitude, -MaxMercatorLatitude, MaxMercatorLatitude);
        var sinLatitude = Math.Sin(clampedLatitude * Math.PI / 180d);
        var x = (longitude + 180d) / 360d * scale;
        var y = (0.5d - Math.Log((1d + sinLatitude) / (1d - sinLatitude)) / (4d * Math.PI)) * scale;
        return (x, y);
    }

    private static (double Latitude, double Longitude) UnprojectFromWorldPixels(double x, double y, double zoom)
    {
        var scale = 256d * Math.Pow(2d, zoom);
        var longitude = x / scale * 360d - 180d;
        var n = Math.PI - 2d * Math.PI * y / scale;
        var latitude = 180d / Math.PI * Math.Atan(Math.Sinh(n));
        return (latitude, longitude);
    }

    private static int SourceRank(string source) => source switch
    {
        "confirmed" => 0,
        "ai" => 1,
        "user" => 2,
        _ => 3
    };

    private static string NormalizeCode(string code) =>
        string.Concat(code.Where(character => !char.IsWhiteSpace(character))).ToLowerInvariant();

    private sealed record DialectInfo(int Id, string Code, int HintOrder);

    private sealed record DialectProjection(
        string[] Codes,
        string Source,
        bool IsMeaningful,
        DialectTier WinningTier);

    private sealed record ProjectedRecording(
        MapRecordingCandidate Candidate,
        string[] DialectCodes,
        string Source);

    private sealed record CellGroupKey(
        long CellX,
        long CellY,
        string Source,
        string DialectSignature);

    private enum DialectTier
    {
        None,
        Confirmed,
        Ai,
        User
    }
}
