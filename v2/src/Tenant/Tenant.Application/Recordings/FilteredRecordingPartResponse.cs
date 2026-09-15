namespace Tenant.Application.Recordings;

public record FilteredRecordingPartResponse(
    int Id,
    DateTime StartDate,
    DateTime EndDate,
    short? State,
    bool? RepresentantFlag,
    int RecordingId,
    RecordingResponse Recording,
    DetectedDialectResponse[] DetectedDialects);

public record DetectedDialectResponse(
    int Id,
    string? UserGuessDialect,
    string? ConfirmedDialect,
    string? PredictedDialect,
    int FilteredRecordingPartId);
