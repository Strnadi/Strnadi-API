using Microsoft.EntityFrameworkCore;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Infrastructure.Persistence.Repositories;

public class MapPointsRepository(TenantDbContext db) : IMapPointsRepository
{
    public async Task<MapRecordingCandidate[]> GetInBoundsAsync(
        MapBounds bounds,
        MapPointFilters filters,
        CancellationToken cancellationToken = default)
    {
        var recordingsQuery = db.Recordings
            .AsNoTracking()
            .Where(recording => recording.Deleted != true);

        recordingsQuery = filters.OwnerScope switch
        {
            MapOwnerScope.Mine => recordingsQuery.Where(recording => recording.UserId == filters.UserId),
            MapOwnerScope.Others => recordingsQuery.Where(recording =>
                recording.UserId == null || recording.UserId != filters.UserId),
            _ => recordingsQuery
        };

        if (filters.CreatedFromUtc is not null)
            recordingsQuery = recordingsQuery.Where(recording => recording.CreatedAt >= filters.CreatedFromUtc.Value);

        if (filters.CreatedToUtc is not null)
            recordingsQuery = recordingsQuery.Where(recording => recording.CreatedAt < filters.CreatedToUtc.Value);

        var candidateQuery = recordingsQuery
            .Select(recording => new
            {
                Recording = recording,
                RepresentativePart = recording.RecordingParts
                    .Where(part =>
                        part.EndDate != null &&
                        part.GpsLatitudeEnd != null &&
                        part.GpsLongitudeEnd != null &&
                        part.GpsLatitudeEnd >= -90 && part.GpsLatitudeEnd <= 90 &&
                        part.GpsLongitudeEnd >= -180 && part.GpsLongitudeEnd <= 180)
                    .OrderByDescending(part => part.EndDate)
                    .ThenByDescending(part => part.Id)
                    .Select(part => new
                    {
                        PartId = (int?)part.Id,
                        part.GpsLatitudeEnd,
                        part.GpsLongitudeEnd
                    })
                    .FirstOrDefault()
            })
            .Where(candidate => candidate.RepresentativePart != null);

        var south = (decimal)bounds.South;
        var north = (decimal)bounds.North;
        var west = (decimal)bounds.West;
        var east = (decimal)bounds.East;

        candidateQuery = candidateQuery
            .Where(candidate => candidate.RepresentativePart!.GpsLatitudeEnd >= south)
            .Where(candidate => candidate.RepresentativePart!.GpsLatitudeEnd <= north);

        candidateQuery = bounds.West <= bounds.East
            ? candidateQuery.Where(candidate =>
                candidate.RepresentativePart!.GpsLongitudeEnd >= west &&
                candidate.RepresentativePart.GpsLongitudeEnd <= east)
            : candidateQuery.Where(candidate =>
                candidate.RepresentativePart!.GpsLongitudeEnd >= west ||
                candidate.RepresentativePart.GpsLongitudeEnd <= east);

        var recordings = await candidateQuery
            .Select(candidate => new
            {
                candidate.Recording.Id,
                RepresentativePartId = candidate.RepresentativePart!.PartId!.Value,
                Latitude = (double)candidate.RepresentativePart.GpsLatitudeEnd!.Value,
                Longitude = (double)candidate.RepresentativePart.GpsLongitudeEnd!.Value,
                candidate.Recording.CreatedAt,
                candidate.Recording.UserId,
                candidate.Recording.Name
            })
            .ToArrayAsync(cancellationToken);

        if (recordings.Length == 0)
            return [];

        var recordingIds = recordings.Select(recording => recording.Id).ToArray();

        var partRanges = await db.RecordingParts
            .AsNoTracking()
            .Where(part =>
                part.RecordingId != null &&
                recordingIds.Contains(part.RecordingId.Value) &&
                part.StartDate != null &&
                part.EndDate != null)
            .Select(part => new
            {
                RecordingId = part.RecordingId!.Value,
                StartDate = part.StartDate!.Value,
                EndDate = part.EndDate!.Value
            })
            .ToArrayAsync(cancellationToken);

        var filteredParts = await db.FilteredRecordingParts
            .AsNoTracking()
            .Where(part => recordingIds.Contains(part.RecordingId))
            .Select(part => new
            {
                part.RecordingId,
                part.StartDate,
                part.EndDate,
                part.RepresentantFlag,
                ConfirmedDialectId = part.DetectedDialect != null
                    ? part.DetectedDialect.ConfirmedDialectId
                    : null,
                PredictedDialectId = part.DetectedDialect != null
                    ? part.DetectedDialect.PredictedDialectId
                    : null,
                UserGuessDialectId = part.DetectedDialect != null
                    ? part.DetectedDialect.UserGuessDialectId
                    : null
            })
            .ToArrayAsync(cancellationToken);

        var rangesByRecording = partRanges
            .GroupBy(part => part.RecordingId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(part => new MapRecordingPartRange(part.StartDate, part.EndDate)).ToArray());

        var filteredByRecording = filteredParts
            .GroupBy(part => part.RecordingId)
            .ToDictionary(
                group => group.Key,
                group => group.Select(part => new MapFilteredPartCandidate(
                    part.StartDate,
                    part.EndDate,
                    part.RepresentantFlag,
                    part.ConfirmedDialectId,
                    part.PredictedDialectId,
                    part.UserGuessDialectId)).ToArray());

        return recordings.Select(recording => new MapRecordingCandidate(
            recording.Id,
            recording.RepresentativePartId,
            recording.Latitude,
            recording.Longitude,
            recording.CreatedAt,
            recording.UserId,
            recording.Name,
            rangesByRecording.GetValueOrDefault(recording.Id) ?? [],
            filteredByRecording.GetValueOrDefault(recording.Id) ?? [])).ToArray();
    }
}
