using Microsoft.EntityFrameworkCore;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Infrastructure.Persistence.Repositories;

public class MapPointsRepository(TenantDbContext db) : IMapPointsRepository
{
    public async Task<RecordingCandidate[]> GetCandidatesInBoundsAsync(MapBounds bounds, MapRecordingFilters filters, CancellationToken cancellationToken = default)
    {
        var south = (decimal)bounds.South;
        var north = (decimal)bounds.North;
        var west = (decimal)bounds.West;
        var east = (decimal)bounds.East;
        bool crossesAntimeridian = bounds.West > bounds.East;

        var partsQuery = db.RecordingParts.AsQueryable();

        // Coarse superset: either endpoint of the part inside the (possibly antimeridian-wrapping)
        // longitude band. Exact per-recording location resolution happens in Application.
        partsQuery = crossesAntimeridian
            ? partsQuery.Where(p =>
                ((p.GpsLatitudeStart >= south && p.GpsLatitudeStart <= north) && (p.GpsLongitudeStart >= west || p.GpsLongitudeStart <= east)) ||
                ((p.GpsLatitudeEnd >= south && p.GpsLatitudeEnd <= north) && (p.GpsLongitudeEnd >= west || p.GpsLongitudeEnd <= east)))
            : partsQuery.Where(p =>
                ((p.GpsLatitudeStart >= south && p.GpsLatitudeStart <= north) && (p.GpsLongitudeStart >= west && p.GpsLongitudeStart <= east)) ||
                ((p.GpsLatitudeEnd >= south && p.GpsLatitudeEnd <= north) && (p.GpsLongitudeEnd >= west && p.GpsLongitudeEnd <= east)));

        var recordingIdsInBounds = await partsQuery
            .Where(p => p.RecordingId != null)
            .Select(p => p.RecordingId!.Value)
            .Distinct()
            .ToArrayAsync(cancellationToken);

        if (recordingIdsInBounds.Length == 0)
            return [];

        var recordingsQuery = db.Recordings
            .Where(r => recordingIdsInBounds.Contains(r.Id))
            .Where(r => r.Deleted != true);

        recordingsQuery = filters.OwnerScope switch
        {
            OwnerScope.Mine => recordingsQuery.Where(r => r.UserId == filters.UserId),
            OwnerScope.Others => recordingsQuery.Where(r => r.UserId != filters.UserId),
            _ => recordingsQuery
        };

        if (filters.CreatedFrom is not null)
        {
            var from = filters.CreatedFrom.Value.ToDateTime(TimeOnly.MinValue);
            recordingsQuery = recordingsQuery.Where(r => r.CreatedAt >= from);
        }

        if (filters.CreatedTo is not null)
        {
            var toExclusive = filters.CreatedTo.Value.AddDays(1).ToDateTime(TimeOnly.MinValue);
            recordingsQuery = recordingsQuery.Where(r => r.CreatedAt < toExclusive);
        }

        var recordings = await recordingsQuery
            .Select(r => new { r.Id, r.UserId, r.CreatedAt, r.Name })
            .ToArrayAsync(cancellationToken);

        if (recordings.Length == 0)
            return [];

        var recordingIds = recordings.Select(r => r.Id).ToArray();

        var representatives = await db.FilteredRecordingParts
            .Where(fp => recordingIds.Contains(fp.RecordingId) && fp.RepresentantFlag == true)
            .Select(fp => new
            {
                fp.RecordingId,
                fp.Id,
                fp.StartDate,
                fp.EndDate,
                fp.State,
                ConfirmedDialectId = fp.DetectedDialect != null ? fp.DetectedDialect.ConfirmedDialectId : null,
                PredictedDialectId = fp.DetectedDialect != null ? fp.DetectedDialect.PredictedDialectId : null,
                UserGuessDialectId = fp.DetectedDialect != null ? fp.DetectedDialect.UserGuessDialectId : null,
            })
            .ToArrayAsync(cancellationToken);

        var parts = await db.RecordingParts
            .Where(p => p.RecordingId != null && recordingIds.Contains(p.RecordingId.Value))
            .Select(p => new { RecordingId = p.RecordingId!.Value, p.Id, p.StartDate, p.EndDate, p.GpsLatitudeEnd, p.GpsLongitudeEnd })
            .ToArrayAsync(cancellationToken);

        var repsByRecording = representatives.ToLookup(r => r.RecordingId);
        var partsByRecording = parts.ToLookup(p => p.RecordingId);

        return recordings.Select(r => new RecordingCandidate(
            r.Id,
            r.UserId,
            r.CreatedAt,
            r.Name,
            repsByRecording[r.Id]
                .Select(x => new RepresentativeCandidate(x.Id, x.StartDate, x.EndDate, x.State, x.ConfirmedDialectId, x.PredictedDialectId, x.UserGuessDialectId))
                .ToArray(),
            partsByRecording[r.Id]
                .Select(x => new RecordingPartLite(x.Id, x.StartDate, x.EndDate, x.GpsLatitudeEnd, x.GpsLongitudeEnd))
                .ToArray()))
            .ToArray();
    }

    public async Task<int[]> GetVisibleRecordingIdsAsync(int[] recordingIds, CancellationToken cancellationToken = default)
    {
        return await db.Recordings
            .Where(r => recordingIds.Contains(r.Id) && r.Deleted != true)
            .Select(r => r.Id)
            .ToArrayAsync(cancellationToken);
    }
}
