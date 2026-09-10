using Tenant.Application.Maps;
using Tenant.Domain.Enums;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Tests;

public class RecordingPointResolverTests
{
    private const int UnknownDialectId = 999;
    private static readonly DateTime Base = new(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static RecordingPartLite Part(int id, int startOffsetMin, int endOffsetMin, decimal? lat, decimal? lng) =>
        new(id, Base.AddMinutes(startOffsetMin), Base.AddMinutes(endOffsetMin), lat, lng);

    private static RepresentativeCandidate Rep(
        int id, int startOffsetMin, int endOffsetMin, short? state,
        int? confirmed = null, int? predicted = null, int? userGuess = null) =>
        new(id, Base.AddMinutes(startOffsetMin), Base.AddMinutes(endOffsetMin), state, confirmed, predicted, userGuess);

    // --- Location resolution ---

    [Fact]
    public void Resolve_UsesOverlappingPartEndGps_ForNewestRepresentative()
    {
        var rep = Rep(1, startOffsetMin: 0, endOffsetMin: 10, state: null);
        var overlappingPart = Part(100, startOffsetMin: -5, endOffsetMin: 5, lat: 50.1m, lng: 14.1m);
        var nonOverlappingPart = Part(101, startOffsetMin: 20, endOffsetMin: 30, lat: 51m, lng: 15m);

        var candidate = new RecordingCandidate(1, Guid.NewGuid(), Base, "rec", [rep], [overlappingPart, nonOverlappingPart]);

        var point = RecordingPointResolver.Resolve(candidate, DialectMode.All, UnknownDialectId);

        Assert.NotNull(point);
        Assert.Equal(LocationSource.Representative, point!.LocationSource);
        Assert.Equal(1, point.RepresentativePartId);
        Assert.Equal(100, point.LocationPartId);
        Assert.Equal(50.1, point.Latitude, 3);
        Assert.Equal(14.1, point.Longitude, 3);
    }

    [Fact]
    public void Resolve_PrefersNewestRepresentativeByEndDateThenId_WhenBothHaveUsableLocations()
    {
        var older = Rep(1, startOffsetMin: 0, endOffsetMin: 10, state: null);
        var newer = Rep(2, startOffsetMin: 20, endOffsetMin: 30, state: null);

        var olderPart = Part(100, startOffsetMin: -5, endOffsetMin: 5, lat: 10m, lng: 10m);
        var newerPart = Part(101, startOffsetMin: 25, endOffsetMin: 35, lat: 20m, lng: 20m);

        var candidate = new RecordingCandidate(1, null, Base, null, [older, newer], [olderPart, newerPart]);

        var point = RecordingPointResolver.Resolve(candidate, DialectMode.All, UnknownDialectId);

        Assert.NotNull(point);
        Assert.Equal(2, point!.RepresentativePartId);
        Assert.Equal(20, point.Latitude);
    }

    [Fact]
    public void Resolve_FallsBackToLatestValidPart_WhenNoRepresentativeHasUsableLocation()
    {
        var rep = Rep(1, startOffsetMin: 0, endOffsetMin: 10, state: null); // no overlapping part
        var part = Part(100, startOffsetMin: 50, endOffsetMin: 60, lat: 33m, lng: 44m);

        var candidate = new RecordingCandidate(1, null, Base, null, [rep], [part]);

        var point = RecordingPointResolver.Resolve(candidate, DialectMode.All, UnknownDialectId);

        Assert.NotNull(point);
        Assert.Equal(LocationSource.LatestPart, point!.LocationSource);
        Assert.Null(point.RepresentativePartId);
        Assert.Equal(100, point.LocationPartId);
    }

    [Fact]
    public void Resolve_ReturnsNull_WhenRecordingHasNoUsableLocationAtAll()
    {
        var candidate = new RecordingCandidate(1, null, Base, null, [], [Part(1, 0, 10, null, null)]);

        var point = RecordingPointResolver.Resolve(candidate, DialectMode.All, UnknownDialectId);

        Assert.Null(point);
    }

    // --- Dialect / admin authority resolution ---

    [Fact]
    public void Resolve_AggregatesTwoDifferentRepresentativesOfSameDialect_AsTwoVotes()
    {
        var reps = new[]
        {
            Rep(1, 0, 10, (short)FilteredRecordingPartState.ConfirmedWithCorrectGuess, confirmed: 5),
            Rep(2, 20, 30, (short)FilteredRecordingPartState.ConfirmedWithCorrectGuess, confirmed: 5),
        };
        var part = Part(100, -5, 35, 1m, 1m);
        var candidate = new RecordingCandidate(1, null, Base, null, reps, [part]);

        var point = RecordingPointResolver.Resolve(candidate, DialectMode.All, UnknownDialectId);

        Assert.NotNull(point);
        Assert.Equal(2, point!.Votes.Length);
        Assert.All(point.Votes, v => Assert.Equal(5, v.DialectId));
        Assert.All(point.Votes, v => Assert.Equal(ContributionSource.Confirmed, v.Source));
    }

    [Fact]
    public void Resolve_AdminRejection_ProducesUnknownVote_WithConfirmedSource_AndBlocksAiUser()
    {
        var rep = Rep(1, 0, 10, (short)FilteredRecordingPartState.UnableToConfirm,
            confirmed: null, predicted: 7, userGuess: 8);
        var part = Part(100, -5, 15, 1m, 1m);
        var candidate = new RecordingCandidate(1, null, Base, null, [rep], [part]);

        var point = RecordingPointResolver.Resolve(candidate, DialectMode.All, UnknownDialectId);

        Assert.NotNull(point);
        var vote = Assert.Single(point!.Votes);
        Assert.Equal(UnknownDialectId, vote.DialectId);
        Assert.Equal(ContributionSource.Confirmed, vote.Source);
        Assert.Equal(ContributionSource.Confirmed, point.Source);
    }

    [Fact]
    public void Resolve_AdminConfirmedOnOneRep_IgnoresOtherStillPendingRepsEntirely()
    {
        var actedRep = Rep(1, 0, 10, (short)FilteredRecordingPartState.ConfirmedWithCorrectGuess, confirmed: 5);
        var pendingRep = Rep(2, 20, 30, (short)FilteredRecordingPartState.AwaitingProcession, predicted: 9, userGuess: 10);
        var part = Part(100, -5, 35, 1m, 1m);
        var candidate = new RecordingCandidate(1, null, Base, null, [actedRep, pendingRep], [part]);

        var point = RecordingPointResolver.Resolve(candidate, DialectMode.All, UnknownDialectId);

        Assert.NotNull(point);
        var vote = Assert.Single(point!.Votes);
        Assert.Equal(5, vote.DialectId);
    }

    [Fact]
    public void Resolve_NotYetAssessed_FallsBackToAi_WhenModeAllowsIt()
    {
        var rep = Rep(1, 0, 10, (short)FilteredRecordingPartState.DetectedByAi, predicted: 7, userGuess: 8);
        var part = Part(100, -5, 15, 1m, 1m);
        var candidate = new RecordingCandidate(1, null, Base, null, [rep], [part]);

        var point = RecordingPointResolver.Resolve(candidate, DialectMode.AiAdmin, UnknownDialectId);

        Assert.NotNull(point);
        var vote = Assert.Single(point!.Votes);
        Assert.Equal(7, vote.DialectId);
        Assert.Equal(ContributionSource.Ai, vote.Source);
    }

    [Fact]
    public void Resolve_AdminOnlyMode_NeverFallsBackToAiOrUser()
    {
        var rep = Rep(1, 0, 10, (short)FilteredRecordingPartState.AwaitingProcession, predicted: 7, userGuess: 8);
        var part = Part(100, -5, 15, 1m, 1m);
        var candidate = new RecordingCandidate(1, null, Base, null, [rep], [part]);

        var point = RecordingPointResolver.Resolve(candidate, DialectMode.AdminOnly, UnknownDialectId);

        Assert.NotNull(point);
        var vote = Assert.Single(point!.Votes);
        Assert.Equal(UnknownDialectId, vote.DialectId);
        Assert.Equal(ContributionSource.Unknown, vote.Source);
    }

    [Fact]
    public void Resolve_NoRepresentativesAtAll_ProducesSingleUnknownVote()
    {
        var part = Part(100, -5, 15, 1m, 1m);
        var candidate = new RecordingCandidate(1, null, Base, null, [], [part]);

        var point = RecordingPointResolver.Resolve(candidate, DialectMode.All, UnknownDialectId);

        Assert.NotNull(point);
        var vote = Assert.Single(point!.Votes);
        Assert.Equal(UnknownDialectId, vote.DialectId);
        Assert.Equal(ContributionSource.Unknown, vote.Source);
    }
}
