namespace Tenant.Domain.Persistence.Repositories;

public record MapRecordingCandidate(
    int RecordingId,
    int RepresentativePartId,
    double Latitude,
    double Longitude,
    DateTime CreatedAt,
    int? UserId,
    string? Name,
    MapRecordingPartRange[] PartRanges,
    MapFilteredPartCandidate[] FilteredParts);

public record MapRecordingPartRange(DateTime StartDate, DateTime EndDate);

public record MapFilteredPartCandidate(
    DateTime StartDate,
    DateTime EndDate,
    bool? RepresentantFlag,
    int? ConfirmedDialectId,
    int? PredictedDialectId,
    int? UserGuessDialectId);
