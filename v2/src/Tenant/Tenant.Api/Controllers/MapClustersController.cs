using Microsoft.AspNetCore.Mvc;
using Tenant.Application.Maps;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("recordings/map-clusters")]
public class MapClustersController(MapClustersService mapClusters) : ControllerBase
{
    /// <summary>Clusters recording points for the current map viewport, given either explicit bounds or a center/zoom.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MapClustersResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] double? centerLat,
        [FromQuery] double? centerLng,
        [FromQuery] double? zoom,
        [FromQuery] int? viewportWidthPx,
        [FromQuery] int? viewportHeightPx,
        [FromQuery] double devicePixelRatio,
        [FromQuery] double? north,
        [FromQuery] double? south,
        [FromQuery] double? east,
        [FromQuery] double? west,
        [FromQuery] DialectMode dialectMode,
        [FromQuery] bool verified,
        [FromQuery] int? userId,
        [FromQuery] DateOnly? createdFrom,
        [FromQuery] DateOnly? createdTo,
        [FromQuery] int maxItemsPerCluster,
        CancellationToken cancellationToken)
    {
        bool hasBounds = north is not null && south is not null && east is not null && west is not null;
        bool hasCenterZoom = centerLat is not null && centerLng is not null && zoom is not null
            && viewportWidthPx is not null && viewportHeightPx is not null;

        if (!hasBounds && !hasCenterZoom)
            return BadRequest(new { error = "invalid_viewport", message = "either bounds or center/zoom/viewport dimensions are required" });

        if (zoom is null && !hasBounds)
            return BadRequest(new { error = "invalid_viewport", message = "zoom is required" });

        var bounds = hasBounds ? new MapBounds(north!.Value, south!.Value, east!.Value, west!.Value) : null;

        if (bounds is not null && (bounds.North <= bounds.South || bounds.East <= bounds.West))
            return UnprocessableEntity(new { error = "invalid_bounds" });

        var query = new MapClustersQuery(
            hasCenterZoom ? new Coords(centerLat!.Value, centerLng!.Value) : null,
            zoom ?? 0,
            viewportWidthPx,
            viewportHeightPx,
            devicePixelRatio == 0 ? 1 : devicePixelRatio,
            bounds,
            dialectMode,
            verified,
            userId,
            createdFrom,
            createdTo,
            maxItemsPerCluster == 0 ? 50 : maxItemsPerCluster);

        return Ok(await mapClusters.GetClustersAsync(query, cancellationToken));
    }
}
