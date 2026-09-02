namespace Strnadi.Application.Recordings;

public record RecordingUploadRequest(
    DateTime CreatedAt,
    short EstimatedBirdsCount,
    string Device,
    string? DeviceId,
    bool ByApp,
    string? Note,
    string? Name,
    int? ExpectedPartsCount);
