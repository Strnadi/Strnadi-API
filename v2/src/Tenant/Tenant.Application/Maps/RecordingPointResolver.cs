using Tenant.Domain.Enums;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Maps;

public enum LocationSource { Representative, LatestPart }

public enum ContributionSource { Confirmed, Ai, User, Unknown }

public record DialectVote(int DialectId, ContributionSource Source);

public record ResolvedRecordingPoint(
    int RecordingId,
    Guid? UserId,
    string? Name,
    DateTime CreatedAt,
    double Latitude,
    double Longitude,
    int? RepresentativePartId,
    int LocationPartId,
    LocationSource LocationSource,
    DialectVote[] Votes,
    ContributionSource Source);

// Pure, DB-free resolution of "where does this recording render, and which dialects does it
// vote for" - see CR-2026-004 "Výběr mapového bodu" / "Výběr dialektů a autorita administrátora".
// No I/O here on purpose: this is the part of the endpoint with the most edge cases, so it needs
// to be unit-testable against the CR's acceptance criteria without a database.
public static class RecordingPointResolver
{
    // "Assessed by admin": FilteredRecordingPart.State values reached only after an admin acted,
    // as opposed to AwaitingProcession/DetectedByAi which are still pending admin review.
    // UnableToConfirm with a null ConfirmedDialectId is the explicit "No Dialect" rejection.
    private static readonly HashSet<short> AdminActedStates =
    [
        (short)FilteredRecordingPartState.ConfirmedWithCorrectGuess,
        (short)FilteredRecordingPartState.ConfirmedWithWrongGuess,
        (short)FilteredRecordingPartState.ConfirmedManually,
        (short)FilteredRecordingPartState.DetectedByAiAndConfirmed,
        (short)FilteredRecordingPartState.UnableToConfirm,
    ];

    public static ResolvedRecordingPoint? Resolve(RecordingCandidate candidate, DialectMode dialectMode, int unknownDialectId)
    {
        var location = ResolveLocation(candidate);
        if (location is null)
            return null;

        var (votes, source) = ResolveVotes(candidate.Representatives, dialectMode, unknownDialectId);

        return new ResolvedRecordingPoint(
            candidate.RecordingId,
            candidate.UserId,
            candidate.Name,
            candidate.CreatedAt,
            location.Value.Latitude,
            location.Value.Longitude,
            location.Value.RepresentativePartId,
            location.Value.LocationPartId,
            location.Value.LocationSource,
            votes,
            source);
    }

    private static (double Latitude, double Longitude, int? RepresentativePartId, int LocationPartId, LocationSource LocationSource)? ResolveLocation(
        RecordingCandidate candidate)
    {
        foreach (var rep in candidate.Representatives.OrderByDescending(r => r.EndDate).ThenByDescending(r => r.Id))
        {
            var match = candidate.Parts
                .Where(p => p.StartDate is not null && p.EndDate is not null &&
                            p.GpsLatitudeEnd is not null && p.GpsLongitudeEnd is not null &&
                            p.StartDate < rep.EndDate && p.EndDate > rep.StartDate)
                .OrderByDescending(p => p.EndDate)
                .ThenByDescending(p => p.Id)
                .FirstOrDefault();

            if (match is not null)
                return ((double)match.GpsLatitudeEnd!.Value, (double)match.GpsLongitudeEnd!.Value, rep.Id, match.Id, LocationSource.Representative);
        }

        var fallback = candidate.Parts
            .Where(p => p.EndDate is not null && p.GpsLatitudeEnd is not null && p.GpsLongitudeEnd is not null)
            .OrderByDescending(p => p.EndDate)
            .ThenByDescending(p => p.Id)
            .FirstOrDefault();

        if (fallback is not null)
            return ((double)fallback.GpsLatitudeEnd!.Value, (double)fallback.GpsLongitudeEnd!.Value, null, fallback.Id, LocationSource.LatestPart);

        return null;
    }

    private static (DialectVote[] Votes, ContributionSource Source) ResolveVotes(
        RepresentativeCandidate[] representatives, DialectMode dialectMode, int unknownDialectId)
    {
        var actedReps = representatives.Where(r => r.State is not null && AdminActedStates.Contains(r.State.Value)).ToArray();

        if (actedReps.Length > 0)
        {
            // Explicit admin decision is final and blocks AI/user, even for a "No Dialect"
            // rejection (ConfirmedDialectId null under UnableToConfirm) - the vote still carries
            // dialectId = the catalog "unknown" row, but source stays "confirmed".
            var votes = actedReps
                .Select(r => new DialectVote(r.ConfirmedDialectId ?? unknownDialectId, ContributionSource.Confirmed))
                .ToArray();
            return (votes, ContributionSource.Confirmed);
        }

        if (dialectMode is DialectMode.All or DialectMode.AiAdmin)
        {
            var aiReps = representatives.Where(r => r.PredictedDialectId is not null).ToArray();
            if (aiReps.Length > 0)
            {
                var votes = aiReps.Select(r => new DialectVote(r.PredictedDialectId!.Value, ContributionSource.Ai)).ToArray();
                return (votes, ContributionSource.Ai);
            }
        }

        if (dialectMode == DialectMode.All)
        {
            var userReps = representatives.Where(r => r.UserGuessDialectId is not null).ToArray();
            if (userReps.Length > 0)
            {
                var votes = userReps.Select(r => new DialectVote(r.UserGuessDialectId!.Value, ContributionSource.User)).ToArray();
                return (votes, ContributionSource.User);
            }
        }

        return ([new DialectVote(unknownDialectId, ContributionSource.Unknown)], ContributionSource.Unknown);
    }
}
