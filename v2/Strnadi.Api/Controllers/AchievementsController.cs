using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Achievements;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("achievements")]
public class AchievementsController(AchievementsService achievementsService) : ControllerBase
{
    /// <summary>All achievements, or just the ones this user earned (awards any newly qualified ones first).</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] int? userId, CancellationToken cancellationToken)
    {
        return Ok(await achievementsService.GetAllAsync(userId, cancellationToken));
    }

    /// <summary>The achievement's icon, as a PNG.</summary>
    [HttpGet("{achievementId:int}/photo")]
    public async Task<IActionResult> GetPhotoAsync([FromRoute] int achievementId, CancellationToken cancellationToken)
    {
        var bytes = await achievementsService.GetPhotoAsync(achievementId, cancellationToken);
        return File(bytes, "image/png");
    }

    /// <summary>Defines a new achievement: an SQL rule for who earns it, localized text, and an icon. Admin only.</summary>
    // TODO(security): sql field is executed as raw SQL server-side - see backend-review.md.
    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [RequestSizeLimit(int.MaxValue)]
    public async Task<IActionResult> CreateAsync([FromForm] string sql, [FromForm] string contents, IFormFile file, CancellationToken cancellationToken)
    {
        List<AchievementContentRequest>? parsedContents;
        try
        {
            parsedContents = JsonSerializer.Deserialize<List<AchievementContentRequest>>(contents);
        }
        catch (JsonException ex)
        {
            return BadRequest($"Invalid contents format: {ex.Message}");
        }

        if (parsedContents is null || parsedContents.Count == 0)
            return BadRequest("Contents is required");

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream, cancellationToken);

        var id = await achievementsService.CreateAsync(sql, parsedContents, stream.ToArray(), cancellationToken);
        return Ok(id);
    }
}
