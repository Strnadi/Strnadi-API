using System.Security.Cryptography;
using System.Text;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Maps;

public class MapClustersService(
    IMapPointsRepository mapPoints,
    IDialectsRepository dialects)
{
    private const double EarthRadiusMeters = 6_371_008.8d;

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
        var dialectsById = (await dialects.GetAllAsync(cancellationToken)).ToDictionary(
            dialect => dialect.Id,
            dialect => new DialectInfo(dialect.Id, dialect.DialectCode, dialect.HintOrder, dialect.IsDialect));

        var visible = new List<ProjectedRecording>(candidates.Length);

        foreach (var candidate in candidates)
        {
            var dialectProjection = ResolveDialects(candidate, query.DialectMode, dialectsById);

            if (query.OnlyMeaningfulDialects && !dialectProjection.IsMeaningful)
                continue;

            var isReferenceOwner = query.UserId is not null && candidate.UserId == query.UserId;
            if (query.HideOthersWithoutMeaningfulDialect && !isReferenceOwner && !dialectProjection.IsMeaningful)
                continue;

            visible.Add(new ProjectedRecording(candidate, dialectProjection.Dialects, dialectProjection.Source));
        }

        var features = query.Clustered
            ? BuildClusteredFeatures(visible, query.ClusterDistanceMeters)
            : visible
                .OrderBy(recording => SourceRank(recording.Source))
                .ThenByDescending(recording => recording.Candidate.CreatedAt)
                .ThenBy(recording => recording.Candidate.RecordingId)
                .Select(ToRecordingFeature)
                .Cast<MapFeature>()
                .ToArray();

        return new MapClustersResult(
            query.Bounds,
            query.Clustered,
            query.Clustered ? query.ClusterDistanceMeters : null,
            visible.Count,
            features);
    }

    private static MapFeature[] BuildClusteredFeatures(
        IReadOnlyCollection<ProjectedRecording> recordings,
        double clusterDistanceMeters)
    {
        return recordings
            .GroupBy(recording => new ClusterKey(
                GetGridCell(recording.Candidate, clusterDistanceMeters),
                recording.Source,
                string.Join('\u001f', recording.Dialects.Select(dialect => dialect.Code))))
            .OrderBy(group => group.Key.Cell.LatitudeBand)
            .ThenBy(group => group.Key.Cell.LongitudeBand)
            .ThenBy(group => SourceRank(group.Key.Source))
            .ThenBy(group => group.Key.DialectSignature, StringComparer.Ordinal)
            .Select(group => BuildFeature(group.Key, group.ToArray(), clusterDistanceMeters))
            .OrderBy(FeatureLatitude)
            .ThenBy(FeatureLongitude)
            .ThenBy(feature => feature is MapClusterFeature ? 0 : 1)
            .ToArray();
    }

    private static GridCell GetGridCell(
        MapRecordingCandidate recording,
        double clusterDistanceMeters)
    {
        var cellHeightRadians = clusterDistanceMeters / EarthRadiusMeters;
        var latitudeRadians = recording.Latitude * Math.PI / 180d;
        var latitudeBand = (long)Math.Floor((latitudeRadians + Math.PI / 2d) / cellHeightRadians);
        var bandCenterLatitude = latitudeBand * cellHeightRadians + cellHeightRadians / 2d - Math.PI / 2d;
        bandCenterLatitude = Math.Clamp(bandCenterLatitude, -Math.PI / 2d, Math.PI / 2d);

        var parallelRadius = EarthRadiusMeters * Math.Max(Math.Cos(bandCenterLatitude), 1e-12d);
        var cellWidthRadians = clusterDistanceMeters / parallelRadius;
        var longitudeRadians = (NormalizeLongitude(recording.Longitude) + 180d) * Math.PI / 180d;
        var longitudeBand = (long)Math.Floor(longitudeRadians / cellWidthRadians);

        return new GridCell(latitudeBand, longitudeBand);
    }

    private static MapFeature BuildFeature(
        ClusterKey key,
        ProjectedRecording[] recordings,
        double clusterDistanceMeters)
    {
        if (recordings.Length == 1)
            return ToRecordingFeature(recordings[0]);

        var orderedItems = recordings
            .OrderBy(recording => SourceRank(recording.Source))
            .ThenByDescending(recording => recording.Candidate.CreatedAt)
            .ThenBy(recording => recording.Candidate.RecordingId)
            .ToArray();

        var (longitude, west, east) = SummarizeLongitudes(
            recordings.Select(recording => recording.Candidate.Longitude).ToArray());
        var bounds = new MapBounds(
            recordings.Max(recording => recording.Candidate.Latitude),
            recordings.Min(recording => recording.Candidate.Latitude),
            east,
            west);

        return new MapClusterFeature(
            CreateClusterId(key, recordings, clusterDistanceMeters),
            recordings.Average(recording => recording.Candidate.Latitude),
            longitude,
            bounds,
            recordings.Length,
            recordings[0].Dialects.Select(dialect => dialect.Code).ToArray(),
            recordings[0].Source,
            orderedItems.Select(ToClusterItem).ToArray());
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
            recording.Dialects.Select(dialect => dialect.Code).ToArray(),
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
        IReadOnlyDictionary<int, DialectInfo> dialectsById)
    {
        var relevantParts = candidate.FilteredParts
            .Where(filtered => candidate.PartRanges.Any(part =>
                filtered.EndDate >= part.StartDate && filtered.StartDate <= part.EndDate))
            .ToArray();

        var confirmed = SelectTier(
            relevantParts.Select(part => part.ConfirmedDialectId),
            DialectTier.Confirmed,
            MapDialectSource.Confirmed,
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
                MapDialectSource.Ai,
                dialectsById);
            if (predicted is not null)
                return predicted;
        }

        if (mode == DialectMode.All)
        {
            var user = SelectTier(
                representativeParts.Select(part => part.UserGuessDialectId),
                DialectTier.User,
                MapDialectSource.User,
                dialectsById);
            if (user is not null)
                return user;
        }

        return new DialectProjection([], MapDialectSource.Unknown, IsMeaningful: false, DialectTier.None);
    }

    private static DialectProjection? SelectTier(
        IEnumerable<int?> dialectIds,
        DialectTier tier,
        MapDialectSource source,
        IReadOnlyDictionary<int, DialectInfo> dialectsById)
    {
        var dialectValues = dialectIds
            .Where(id => id is not null && dialectsById.ContainsKey(id.Value))
            .Select(id => dialectsById[id!.Value])
            .DistinctBy(dialect => dialect.Id)
            .ToArray();

        if (dialectValues.Length == 0)
            return null;

        var meaningful = dialectValues
            .Where(dialect => dialect.IsDialect)
            .ToArray();
        var selected = meaningful.Length > 0 ? meaningful : dialectValues;

        return new DialectProjection(
            selected
                .OrderBy(dialect => dialect.HintOrder)
                .ThenBy(dialect => dialect.Id)
                .ToArray(),
            meaningful.Length > 0 ? source : MapDialectSource.Unknown,
            meaningful.Length > 0,
            tier);
    }

    private static string NormalizeDialectName(string code) =>
        string.Concat(code.Where(char.IsLetterOrDigit));

    private static string CreateClusterId(
        ClusterKey key,
        IEnumerable<ProjectedRecording> recordings,
        double clusterDistanceMeters)
    {
        var recordingIds = recordings
            .Select(recording => recording.Candidate.RecordingId)
            .OrderBy(id => id);
        var identity = FormattableString.Invariant(
            $"{clusterDistanceMeters:R}|{key.Cell.LatitudeBand}|{key.Cell.LongitudeBand}|{key.Source}|{key.DialectSignature}|{string.Join(',', recordingIds)}");
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(identity));
        return $"map_{Convert.ToHexString(hash.AsSpan(0, 12)).ToLowerInvariant()}";
    }

    private static (double Center, double West, double East) SummarizeLongitudes(double[] longitudes)
    {
        var center = Math.Atan2(
            longitudes.Average(longitude => Math.Sin(longitude * Math.PI / 180d)),
            longitudes.Average(longitude => Math.Cos(longitude * Math.PI / 180d))) * 180d / Math.PI;

        var ordered = longitudes
            .Select(longitude => longitude + 180d)
            .OrderBy(longitude => longitude)
            .ToArray();
        var largestGapIndex = 0;
        var largestGap = double.NegativeInfinity;

        for (var index = 0; index < ordered.Length; index++)
        {
            var next = index + 1 < ordered.Length ? ordered[index + 1] : ordered[0] + 360d;
            var gap = next - ordered[index];
            if (gap > largestGap)
            {
                largestGap = gap;
                largestGapIndex = index;
            }
        }

        var west = NormalizeLongitude(ordered[(largestGapIndex + 1) % ordered.Length] - 180d);
        var east = NormalizeLongitude(ordered[largestGapIndex] - 180d);
        return (NormalizeLongitude(center), west, east);
    }

    private static double NormalizeLongitude(double longitude) =>
        (longitude + 540d) % 360d - 180d;

    private static double FeatureLatitude(MapFeature feature) => feature switch
    {
        MapClusterFeature cluster => cluster.Latitude,
        MapRecordingFeature recording => recording.Latitude,
        _ => double.NaN
    };

    private static double FeatureLongitude(MapFeature feature) => feature switch
    {
        MapClusterFeature cluster => cluster.Longitude,
        MapRecordingFeature recording => recording.Longitude,
        _ => double.NaN
    };

    private static int SourceRank(MapDialectSource source) => source switch
    {
        MapDialectSource.Confirmed => 0,
        MapDialectSource.Ai => 1,
        MapDialectSource.User => 2,
        _ => 3
    };

    private sealed record DialectInfo(int Id, string Code, int HintOrder, bool IsDialect);

    private sealed record DialectProjection(
        DialectInfo[] Dialects,
        MapDialectSource Source,
        bool IsMeaningful,
        DialectTier WinningTier);

    private sealed record ProjectedRecording(
        MapRecordingCandidate Candidate,
        DialectInfo[] Dialects,
        MapDialectSource Source);

    private sealed record GridCell(long LatitudeBand, long LongitudeBand);

    private sealed record ClusterKey(
        GridCell Cell,
        MapDialectSource Source,
        string DialectSignature);

    private enum DialectTier
    {
        None,
        Confirmed,
        Ai,
        User
    }
}
