using Microsoft.EntityFrameworkCore;
using Strnadi.Domain.Enums;
using Strnadi.Domain.Persistence.Repositories;

namespace Strnadi.Infrastructure.Persistence.Repositories;

public class MapPointsRepository(AppDbContext db) : IMapPointsRepository
{
    // Same set as FilteredRecordingPartsRepository.VerifiedStates - anything except
    // AwaitingProcession/UnableToConfirm.
    private static readonly short[] VerifiedStates =
    [
        (short)FilteredRecordingPartState.ConfirmedWithCorrectGuess,
        (short)FilteredRecordingPartState.ConfirmedWithWrongGuess,
        (short)FilteredRecordingPartState.ConfirmedManually,
        (short)FilteredRecordingPartState.DetectedByAi,
        (short)FilteredRecordingPartState.DetectedByAiAndConfirmed,
    ];

    public async Task<MapPointCandidate[]> GetInBoundsAsync(MapBounds bounds, MapPointFilters filters, CancellationToken cancellationToken = default)
    {
        var south = (decimal)bounds.South;
        var north = (decimal)bounds.North;
        var west = (decimal)bounds.West;
        var east = (decimal)bounds.East;

        var partsQuery = db.RecordingParts
            .Where(p => p.GpsLatitudeStart != null && p.GpsLongitudeStart != null)
            .Where(p => p.GpsLatitudeStart >= south && p.GpsLatitudeStart <= north)
            .Where(p => p.GpsLongitudeStart >= west && p.GpsLongitudeStart <= east)
            .Where(p => p.Recording != null && p.Recording.Deleted != true);

        if (filters.UserId is not null)
            partsQuery = partsQuery.Where(p => p.Recording!.UserId == filters.UserId);

        if (filters.CreatedFrom is not null)
        {
            var from = filters.CreatedFrom.Value.ToDateTime(TimeOnly.MinValue);
            partsQuery = partsQuery.Where(p => p.Recording!.CreatedAt >= from);
        }

        if (filters.CreatedTo is not null)
        {
            var to = filters.CreatedTo.Value.ToDateTime(TimeOnly.MaxValue);
            partsQuery = partsQuery.Where(p => p.Recording!.CreatedAt <= to);
        }

        var parts = await partsQuery
            .Where(p => p.StartDate != null && p.EndDate != null)
            .Select(p => new
            {
                p.Id,
                RecordingId = p.RecordingId!.Value,
                Latitude = (double)p.GpsLatitudeStart!.Value,
                Longitude = (double)p.GpsLongitudeStart!.Value,
                StartDate = p.StartDate!.Value,
                EndDate = p.EndDate!.Value,
                p.Recording!.CreatedAt,
            })
            .ToArrayAsync(cancellationToken);

        if (parts.Length == 0)
            return [];

        var recordingIds = parts.Select(p => p.RecordingId).Distinct().ToArray();

        var filteredParts = await db.FilteredRecordingParts
            .Where(fp => recordingIds.Contains(fp.RecordingId))
            .Select(fp => new
            {
                fp.RecordingId,
                fp.StartDate,
                fp.EndDate,
                fp.State,
                ConfirmedDialectId = fp.DetectedDialect != null ? fp.DetectedDialect.ConfirmedDialectId : null,
                PredictedDialectId = fp.DetectedDialect != null ? fp.DetectedDialect.PredictedDialectId : null,
                UserGuessDialectId = fp.DetectedDialect != null ? fp.DetectedDialect.UserGuessDialectId : null,
            })
            .ToArrayAsync(cancellationToken);

        var result = new List<MapPointCandidate>(parts.Length);

        foreach (var part in parts)
        {
            var overlapping = filteredParts.Where(fp =>
                fp.RecordingId == part.RecordingId &&
                fp.StartDate < part.EndDate &&
                fp.EndDate > part.StartDate);

            if (filters.Verified)
                overlapping = overlapping.Where(fp => fp.State != null && VerifiedStates.Contains(fp.State.Value));

            var match = overlapping.FirstOrDefault();

            if (filters.Verified && match is null)
                continue;

            result.Add(new MapPointCandidate(
                part.RecordingId,
                part.Id,
                part.Latitude,
                part.Longitude,
                part.CreatedAt,
                match?.ConfirmedDialectId,
                match?.PredictedDialectId,
                match?.UserGuessDialectId));
        }

        return result.ToArray();
    }
}
