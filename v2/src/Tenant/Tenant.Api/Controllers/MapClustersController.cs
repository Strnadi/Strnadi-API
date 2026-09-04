using Microsoft.AspNetCore.Mvc;
using Tenant.Application.Maps;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("recordings/map-clusters")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class MapClustersController(MapClustersService mapClusters) : ControllerBase
{
    private const double MaxMercatorLatitude = 85.05112878;

    /// <summary>Returns recording points, optionally clustered, for an exact Web Mercator viewport.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MapClustersResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] double? north = null,
        [FromQuery] double? south = null,
        [FromQuery] double? east = null,
        [FromQuery] double? west = null,
        [FromQuery] double? zoom = null,
        [FromQuery] bool clustered = false,
        [FromQuery] int? clusterResolutionPx = null,
        [FromQuery] MapOwnerScope ownerScope = MapOwnerScope.All,
        [FromQuery] int? userId = null,
        [FromQuery] DateTimeOffset? createdFromUtc = null,
        [FromQuery] DateTimeOffset? createdToUtc = null,
        [FromQuery] bool onlyMeaningfulDialects = false,
        [FromQuery] bool hideOthersWithoutMeaningfulDialect = true,
        [FromQuery] DialectMode dialectMode = DialectMode.All,
        CancellationToken cancellationToken = default)
    {
        if (north is null || south is null || east is null || west is null || zoom is null)
            return InvalidQuery("missing_viewport", "north, south, east, west, and zoom are required.");

        if (!AreFinite(north.Value, south.Value, east.Value, west.Value, zoom.Value))
            return InvalidQuery("invalid_number", "Viewport bounds and zoom must be finite numbers.");

        if (north is > MaxMercatorLatitude or < -MaxMercatorLatitude ||
            south is > MaxMercatorLatitude or < -MaxMercatorLatitude)
        {
            return InvalidQuery(
                "invalid_latitude",
                $"north and south must be between {-MaxMercatorLatitude} and {MaxMercatorLatitude}.");
        }

        if (east is > 180 or < -180 || west is > 180 or < -180)
            return InvalidQuery("invalid_longitude", "east and west must be between -180 and 180.");

        if (north <= south)
            return InvalidQuery("invalid_bounds", "north must be greater than south.");

        // west > east deliberately represents a viewport crossing the antimeridian.
        if (west == east)
            return InvalidQuery("invalid_bounds", "west and east must not be equal.");

        if (zoom is < 5 or > 19)
            return InvalidQuery("invalid_zoom", "zoom must be between 5 and 19.");

        if (!Enum.IsDefined(ownerScope))
            return InvalidQuery("invalid_owner_scope", "ownerScope must be all, mine, or others.");

        if (!Enum.IsDefined(dialectMode))
            return InvalidQuery("invalid_dialect_mode", "dialectMode must be all, aiAdmin, or adminOnly.");

        if (userId is <= 0)
            return InvalidQuery("invalid_user_id", "userId must be a positive integer.");

        if ((ownerScope is MapOwnerScope.Mine or MapOwnerScope.Others) && userId is null)
            return InvalidQuery("missing_user_id", "userId is required when ownerScope is mine or others.");

        if (clustered && clusterResolutionPx is null)
            return InvalidQuery("missing_cluster_resolution", "clusterResolutionPx is required when clustered is true.");

        if (clusterResolutionPx is not null && (clusterResolutionPx is < 24 or > 96))
            return InvalidQuery("invalid_cluster_resolution", "clusterResolutionPx must be between 24 and 96.");

        if (createdFromUtc is not null && createdToUtc is not null && createdFromUtc >= createdToUtc)
        {
            return InvalidQuery(
                "invalid_date_range",
                "createdFromUtc must be earlier than the exclusive createdToUtc value.");
        }

        var query = new MapClustersQuery(
            new MapBounds(north.Value, south.Value, east.Value, west.Value),
            zoom.Value,
            clustered,
            clusterResolutionPx,
            ownerScope,
            userId,
            createdFromUtc?.UtcDateTime,
            createdToUtc?.UtcDateTime,
            onlyMeaningfulDialects,
            hideOthersWithoutMeaningfulDialect,
            dialectMode);

        return Ok(await mapClusters.GetClustersAsync(query, cancellationToken));
    }

    private BadRequestObjectResult InvalidQuery(string code, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = StatusCodes.Status400BadRequest,
            Title = "Invalid map query",
            Detail = detail,
            Instance = HttpContext.Request.Path
        };
        problem.Extensions["code"] = code;
        return BadRequest(problem);
    }

    private static bool AreFinite(params double[] values) => values.All(double.IsFinite);
}
