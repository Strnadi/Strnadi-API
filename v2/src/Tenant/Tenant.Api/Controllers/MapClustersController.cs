using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Tenant.Application.Maps;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("recordings/map-clusters")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public class MapClustersController(MapClustersService mapClusters) : ControllerBase
{
    /// <summary>
    /// Returns recording points for an exact map viewport. When clustering is enabled,
    /// <paramref name="clusterDistanceMeters"/> defines the approximate ground length of each
    /// grid square's sides; the client can derive it from its own viewport.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(MapClustersResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<MapClustersResult>> GetAsync(
        [FromQuery, BindRequired, Range(-90d, 90d)] double north,
        [FromQuery, BindRequired, Range(-90d, 90d)] double south,
        [FromQuery, BindRequired, Range(-180d, 180d)] double east,
        [FromQuery, BindRequired, Range(-180d, 180d)] double west,
        [FromQuery, BindRequired, Range(double.Epsilon, double.MaxValue)] double clusterDistanceMeters,
        [FromQuery] bool clustered = false,
        [FromQuery] MapOwnerScope ownerScope = MapOwnerScope.All,
        [FromQuery] int? userId = null,
        [FromQuery] DateTimeOffset? createdFromUtc = null,
        [FromQuery] DateTimeOffset? createdToUtc = null,
        [FromQuery] bool onlyMeaningfulDialects = false,
        [FromQuery] bool hideOthersWithoutMeaningfulDialect = true,
        [FromQuery] DialectMode dialectMode = DialectMode.All,
        CancellationToken cancellationToken = default)
    {
        var query = new MapClustersQuery(
            new MapBounds(north, south, east, west),
            clustered,
            clusterDistanceMeters,
            ownerScope,
            userId,
            createdFromUtc?.UtcDateTime,
            createdToUtc?.UtcDateTime,
            onlyMeaningfulDialects,
            hideOthersWithoutMeaningfulDialect,
            dialectMode);

        return Ok(await mapClusters.GetClustersAsync(query, cancellationToken));
    }
}
