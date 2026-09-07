namespace Tenant.Application.Recordings;

public record RecordingResponse(
    int Id,
    DateTime CreatedAt,
    short? EstimatedBirdsCount,
    bool ByApp,
    string? Name,
    string? Note,
    string? NotePost,
    string? Device,
    int? UserId,
    int? ExpectedPartsCount,
    bool UploadConfirmed,
    RecordingPartResponse[]? Parts);

public record RecordingPartResponse(
    int Id,
    DateTime? StartDate,
    DateTime? EndDate,
    decimal? GpsLatitudeStart,
    decimal? GpsLongitudeStart,
    decimal? GpsLatitudeEnd,
    decimal? GpsLongitudeEnd,
    int? Length,
    string? AudioBase64);
