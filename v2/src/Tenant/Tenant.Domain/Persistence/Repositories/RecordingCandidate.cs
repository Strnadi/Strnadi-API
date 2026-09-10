namespace Tenant.Domain.Persistence.Repositories;

// One RepresentantFlag=true FilteredRecordingPart of a candidate recording, with everything the
// admin-authority/dialect-tier resolution in Application needs. State mirrors
// Tenant.Domain.Enums.FilteredRecordingPartState as a raw short (nullable in the DB).
public record RepresentativeCandidate(
    int Id,
    DateTime StartDate,
    DateTime EndDate,
    short? State,
    int? ConfirmedDialectId,
    int? PredictedDialectId,
    int? UserGuessDialectId);

// A RecordingPart of a candidate recording, kept lightweight for the location-fallback join.
public record RecordingPartLite(
    int Id,
    DateTime? StartDate,
    DateTime? EndDate,
    decimal? GpsLatitudeEnd,
    decimal? GpsLongitudeEnd);

// Raw material for a single recording's map presence: every RepresentantFlag=true representative
// (for dialect voting) plus every RecordingPart (for GPS location resolution). Location resolution
// and dialect-tier selection both happen later, in Application (RecordingPointResolver), since both
// require cross-referencing representatives against parts and against the caller's DialectMode.
public record RecordingCandidate(
    int RecordingId,
    Guid? UserId,
    DateTime CreatedAt,
    string? Name,
    RepresentativeCandidate[] Representatives,
    RecordingPartLite[] Parts);
