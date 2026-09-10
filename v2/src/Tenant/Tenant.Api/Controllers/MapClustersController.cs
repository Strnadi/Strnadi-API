using Microsoft.AspNetCore.Mvc;
using Tenant.Application.Maps;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("recordings/map-clusters")]
public class MapClustersController(MapClustersService mapClusters) : ControllerBase
{
    private const double MaxLatitude = 85.05112878;

    /// <summary>Returns clustered/unclustered recording points for the current map viewport.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(MapClustersResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status422UnprocessableEntity)]
    [ProducesResponseType(typeof(object), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> GetAsync(
        [FromQuery] double? centerLat,
        [FromQuery] double? centerLng,
        [FromQuery] double? zoom,
        [FromQuery] int? viewportWidthPx,
        [FromQuery] int? viewportHeightPx,
        [FromQuery] double? devicePixelRatio,
        [FromQuery] double? north,
        [FromQuery] double? south,
        [FromQuery] double? east,
        [FromQuery] double? west,
        [FromQuery] bool? clustered,
        [FromQuery] bool? mixDialects,
        [FromQuery] bool? mixSources,
        [FromQuery] double? clusterDistanceMeters,
        [FromQuery] OwnerScope? ownerScope,
        [FromQuery] Guid? userId,
        [FromQuery] DateOnly? createdFrom,
        [FromQuery] DateOnly? createdTo,
        [FromQuery] bool? onlyMeaningfulDialects,
        [FromQuery] bool? hideOthersWithoutMeaningfulDialect,
        [FromQuery] DialectMode? dialectMode,
        [FromQuery] string? verified,
        [FromQuery] string? maxItemsPerCluster,
        CancellationToken cancellationToken)
    {
        if (verified is not null)
            return Error(400, "unsupported_parameter", "'verified' is no longer supported by this endpoint.");

        if (maxItemsPerCluster is not null)
            return Error(400, "unsupported_parameter", "'maxItemsPerCluster' is no longer supported; the preview limit is fixed at 5.");

        bool hasBounds = north is not null || south is not null || east is not null || west is not null;
        if (hasBounds && (north is null || south is null || east is null || west is null))
            return Error(400, "invalid_viewport", "All of north, south, east and west are required together.");

        bool hasCenterVariant = centerLat is not null || centerLng is not null || viewportWidthPx is not null || viewportHeightPx is not null;
        if (hasCenterVariant && (centerLat is null || centerLng is null || viewportWidthPx is null || viewportHeightPx is null))
            return Error(400, "invalid_viewport", "centerLat, centerLng, viewportWidthPx and viewportHeightPx are required together.");

        if (!hasBounds && !hasCenterVariant)
            return Error(400, "invalid_viewport", "Either bounds (north/south/east/west) or center/zoom/viewport dimensions are required.");

        bool clusteredValue = clustered ?? true;

        if (hasBounds)
        {
            if (south!.Value < -90 || south.Value > 90 || north!.Value < -90 || north.Value > 90)
                return Error(400, "invalid_viewport", "south/north must be within [-90, 90].");
            if (south.Value >= north.Value)
                return Error(400, "invalid_viewport", "south must be less than north.");
            if (east!.Value < -180 || east.Value > 180 || west!.Value < -180 || west.Value > 180)
                return Error(400, "invalid_viewport", "east/west must be within [-180, 180].");
            if (west.Value == east.Value)
                return Error(400, "invalid_viewport", "west and east must not be equal.");

            if (zoom is null && clusteredValue && clusterDistanceMeters is null)
                return Error(400, "invalid_parameter", "zoom or clusterDistanceMeters is required when clustering with explicit bounds.");
        }
        else
        {
            if (centerLat!.Value < -MaxLatitude || centerLat.Value > MaxLatitude)
                return Error(400, "invalid_viewport", $"centerLat must be within [-{MaxLatitude}, {MaxLatitude}].");
            if (centerLng!.Value < -180 || centerLng.Value > 180)
                return Error(400, "invalid_viewport", "centerLng must be within [-180, 180].");
            if (zoom is null)
                return Error(400, "invalid_viewport", "zoom is required for the center/viewport variant.");
            if (viewportWidthPx!.Value <= 0 || viewportHeightPx!.Value <= 0)
                return Error(400, "invalid_viewport", "viewportWidthPx and viewportHeightPx must be positive.");
        }

        if (zoom is not null && (zoom.Value < 0 || zoom.Value > 22))
            return Error(400, "invalid_viewport", "zoom must be within [0, 22].");

        double devicePixelRatioValue = devicePixelRatio ?? 1;
        if (devicePixelRatioValue <= 0)
            return Error(400, "invalid_parameter", "devicePixelRatio must be positive.");

        if (clusterDistanceMeters is not null && clusterDistanceMeters.Value <= 0)
            return Error(400, "invalid_parameter", "clusterDistanceMeters must be positive.");

        var ownerScopeValue = ownerScope ?? OwnerScope.All;
        if (ownerScopeValue is OwnerScope.Mine or OwnerScope.Others && userId is null)
            return Error(400, "invalid_parameter", "userId is required when ownerScope is Mine or Others.");

        if (createdFrom is not null && createdTo is not null && createdFrom.Value > createdTo.Value)
            return UnprocessableEntity(new { error = "invalid_date_range", message = "createdFrom must not be after createdTo.", traceId = HttpContext.TraceIdentifier });

        var bounds = hasBounds ? new MapBounds(north!.Value, south!.Value, east!.Value, west!.Value) : null;

        var query = new MapClustersQuery(
            hasBounds ? null : new Coords(centerLat!.Value, centerLng!.Value),
            zoom,
            hasBounds ? null : viewportWidthPx,
            hasBounds ? null : viewportHeightPx,
            devicePixelRatioValue,
            bounds,
            clusteredValue,
            mixDialects ?? true,
            mixSources ?? true,
            clusterDistanceMeters,
            ownerScopeValue,
            userId,
            createdFrom,
            createdTo,
            onlyMeaningfulDialects ?? false,
            hideOthersWithoutMeaningfulDialect ?? false,
            dialectMode ?? DialectMode.All);

        try
        {
            return Ok(await mapClusters.GetClustersAsync(query, cancellationToken));
        }
        catch (CatalogInconsistentException ex)
        {
            return Error(500, "catalog_inconsistent", ex.Message);
        }
    }

    /// <summary>Pages through a cluster's members beyond the 5-item overview preview.</summary>
    [HttpGet("{clusterId}/items")]
    [ProducesResponseType(typeof(ClusterItemsPage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(object), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(object), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(object), StatusCodes.Status410Gone)]
    public async Task<IActionResult> GetItemsAsync(
        [FromRoute] string clusterId,
        [FromQuery] string? cursor,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(cursor))
            return Error(400, "invalid_cursor", "cursor is required.");

        int pageSizeValue = pageSize ?? 5;
        if (pageSizeValue < 1 || pageSizeValue > 50)
            return Error(400, "invalid_parameter", "pageSize must be within [1, 50].");

        var page = mapClusters.GetClusterItemsPage(clusterId, cursor, pageSizeValue);
        if (page is null)
            return Error(410, "cluster_expired", "This cluster snapshot has expired or no longer exists; re-fetch the overview.");

        if (!await mapClusters.ClusterMembersStillVisibleAsync(clusterId, cancellationToken))
            return Error(409, "cluster_changed", "This cluster's membership changed since the overview was fetched; re-fetch the overview.");

        return Ok(page);
    }

    private IActionResult Error(int status, string error, string message) =>
        StatusCode(status, new { error, message, traceId = HttpContext.TraceIdentifier });
}
