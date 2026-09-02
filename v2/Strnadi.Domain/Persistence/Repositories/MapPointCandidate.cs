namespace Strnadi.Domain.Persistence.Repositories;

// Raw material for clustering: one recording part with a GPS fix, plus all three dialect id tiers
// found for it (via the recording's filtered/detected dialect data). Which tier "wins" is decided
// later, by Application, per the caller's DialectMode.
public record MapPointCandidate(
    int RecordingId,
    int PartId,
    double Latitude,
    double Longitude,
    DateTime CreatedAt,
    int? ConfirmedDialectId,
    int? PredictedDialectId,
    int? UserGuessDialectId);
