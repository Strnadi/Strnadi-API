namespace Strnadi.Application.Recordings;

public record UpdateDetectedDialectRequest(int Id, int? UserGuessDialectId, int? ConfirmedDialectId, int? PredictedDialectId);
