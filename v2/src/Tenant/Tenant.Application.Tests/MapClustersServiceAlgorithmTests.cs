using Tenant.Application.Maps;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Tests;

public class MapClustersServiceAlgorithmTests
{
    // --- Largest-remainder percentage rounding ---

    [Fact]
    public void LargestRemainderPercentages_MatchesCrExample_AA_B_B_Unknown()
    {
        // [A,A,B],[B],[] -> A=2, B=2, unknown=1 -> 40/40/20
        var result = MapClustersService.LargestRemainderPercentages([2, 2, 1]);

        Assert.Equal([40.0, 40.0, 20.0], result);
    }

    [Fact]
    public void LargestRemainderPercentages_OnlyUnknown_Is100Percent()
    {
        var result = MapClustersService.LargestRemainderPercentages([1]);

        Assert.Equal([100.0], result);
    }

    [Fact]
    public void LargestRemainderPercentages_AlwaysSumsToExactly100_EvenWithTies()
    {
        var result = MapClustersService.LargestRemainderPercentages([1, 1, 1]);

        Assert.Equal(100.0, Math.Round(result.Sum(), 2));
        // Tie-break keeps the earlier (pre-sorted) entry ahead on equal remainders.
        Assert.True(result[0] >= result[1]);
        Assert.True(result[1] >= result[2]);
    }

    [Fact]
    public void LargestRemainderPercentages_TwoToOneRatio_RoundsDeterministically()
    {
        var result = MapClustersService.LargestRemainderPercentages([2, 1]);

        Assert.Equal(100.0, Math.Round(result.Sum(), 2));
        Assert.Equal([66.67, 33.33], result);
    }

    // --- Dialect aggregation (sorting + percentage wiring) ---

    [Fact]
    public void AggregateDialects_SortsByHintOrderThenId_AndComputesPercentages()
    {
        var unknown = new Dialect { Id = 999, DialectCode = "unknown", Color = "#000000", HintOrder = 0, IsDialect = false };
        var a = new Dialect { Id = 1, DialectCode = "A", Color = "#111111", HintOrder = 1, IsDialect = true };
        var b = new Dialect { Id = 2, DialectCode = "B", Color = "#222222", HintOrder = 2, IsDialect = true };
        var catalog = new Dictionary<int, Dialect> { [999] = unknown, [1] = a, [2] = b };

        DialectVote[] votes =
        [
            new(1, ContributionSource.Confirmed), new(1, ContributionSource.Confirmed),
            new(2, ContributionSource.Confirmed), new(2, ContributionSource.Confirmed),
            new(999, ContributionSource.Unknown),
        ];

        var result = MapClustersService.AggregateDialects(votes, catalog);

        Assert.Equal(3, result.Length);
        Assert.Equal(["unknown", "A", "B"], result.Select(r => r.DialectCode).ToArray());
        Assert.Equal(999, result[0].Id);
        Assert.False(result[0].IsDialect);
        Assert.Equal(1, result[0].ContributionCount);
        Assert.Equal(20.0, result[0].Percentage);
        Assert.Equal(2, result[1].ContributionCount);
        Assert.Equal(40.0, result[1].Percentage);
        Assert.Equal(2, result[2].ContributionCount);
        Assert.Equal(40.0, result[2].Percentage);
    }

    // --- Bounds / antimeridian ---

    [Theory]
    [InlineData(0, 0, true)]
    [InlineData(45, 0, false)] // outside north/south
    [InlineData(0, 15, false)] // outside east
    [InlineData(0, -15, false)] // outside west
    public void IsInBounds_NonWrappingBounds(double lat, double lng, bool expected)
    {
        var bounds = new MapBounds(North: 10, South: -10, East: 10, West: -10);

        Assert.Equal(expected, MapClustersService.IsInBounds(lat, lng, bounds));
    }

    [Theory]
    [InlineData(0, 179, true)]   // just west of antimeridian, inside band
    [InlineData(0, -179, true)]  // just east of antimeridian, inside band
    [InlineData(0, 0, false)]    // far side of the globe, outside the wrapping band
    public void IsInBounds_AntimeridianWrappingBounds(double lat, double lng, bool expected)
    {
        // west=170, east=-170 means the visible band wraps through +/-180.
        var bounds = new MapBounds(North: 10, South: -10, East: -170, West: 170);

        Assert.Equal(expected, MapClustersService.IsInBounds(lat, lng, bounds));
    }

    // --- Clustering / grouping ---

    private static ResolvedRecordingPoint Point(int recordingId, double lat, double lng, params DialectVote[] votes) =>
        new(recordingId, null, null, DateTime.UtcNow, lat, lng, null, 1, LocationSource.LatestPart, votes, ContributionSource.Confirmed);

    [Fact]
    public void GroupIntoCells_GroupsNearbyPoints_AndSeparatesFarPoints()
    {
        // A tiny offset can never cross a grid-cell boundary; a 170-degree offset always will.
        var near1 = Point(1, 0, 0, new DialectVote(1, ContributionSource.Confirmed));
        var near2 = Point(2, 0, 0.0000001, new DialectVote(1, ContributionSource.Confirmed));
        var far = Point(3, 0, 170, new DialectVote(1, ContributionSource.Confirmed));

        var bounds = new MapBounds(North: 10, South: -10, East: 180, West: -180);
        var groups = MapClustersService.GroupIntoCells([near1, near2, far], zoom: 5, cellPx: 50, bounds, mixDialects: true, mixSources: true)
            .Select(g => g.Select(p => p.RecordingId).OrderBy(id => id).ToArray())
            .OrderBy(g => g[0])
            .ToArray();

        Assert.Equal(2, groups.Length);
        Assert.Equal([1, 2], groups[0]);
        Assert.Equal([3], groups[1]);
    }

    [Fact]
    public void GroupIntoCells_MixDialectsFalse_SplitsSameCellByDialectSet()
    {
        var pointA = Point(1, 0, 0, new DialectVote(1, ContributionSource.Confirmed));
        var pointB = Point(2, 0, 0.0000001, new DialectVote(2, ContributionSource.Confirmed));

        var bounds = new MapBounds(North: 10, South: -10, East: 10, West: -10);
        var groups = MapClustersService.GroupIntoCells([pointA, pointB], zoom: 5, cellPx: 50, bounds, mixDialects: false, mixSources: true)
            .ToArray();

        Assert.Equal(2, groups.Length);
        Assert.All(groups, g => Assert.Single(g));
    }

    [Fact]
    public void GroupIntoCells_MixDialectsTrue_KeepsSameCellTogetherDespiteDifferentDialects()
    {
        var pointA = Point(1, 0, 0, new DialectVote(1, ContributionSource.Confirmed));
        var pointB = Point(2, 0, 0.0000001, new DialectVote(2, ContributionSource.Confirmed));

        var bounds = new MapBounds(North: 10, South: -10, East: 10, West: -10);
        var groups = MapClustersService.GroupIntoCells([pointA, pointB], zoom: 5, cellPx: 50, bounds, mixDialects: true, mixSources: true)
            .ToArray();

        Assert.Single(groups);
        Assert.Equal(2, groups[0].Length);
    }
}
