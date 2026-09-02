namespace Strnadi.Application.Recordings;

public record DetectedDialectUploadRequest(int FilteredPartId, int? UserGuessDialectId, int? ConfirmedDialectId, int? PredictedDialectId);
