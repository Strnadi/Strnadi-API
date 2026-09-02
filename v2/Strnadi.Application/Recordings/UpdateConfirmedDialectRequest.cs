namespace Strnadi.Application.Recordings;

public record UpdateConfirmedDialectRequest(
    int FilteredPartId,
    DateTime? StartDate,
    DateTime? EndDate,
    string? ConfirmedDialectCode,
    bool? Representant);
