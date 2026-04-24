using System.Text.Json;
using Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository;
using Shared.Extensions;
using Shared.Models.Database.Achievements;
using Shared.Models.Requests.Achievements;

namespace Achievements;

/// <summary>
/// Handles achievement listing, image retrieval, and administrative creation.
/// </summary>
[ApiController]
[Route("achievements")]
public class AchievementsController : ControllerBase
{
    /// <summary>
    /// Gets all achievements or the achievements awarded to a specific user.
    /// </summary>
    /// <param name="userId">Optional user identifier used to filter awarded achievements.</param>
    /// <param name="repo">Repository used to query and award achievements.</param>
    /// <returns>The matching achievements.</returns>
    [HttpGet]
    [Produces("application/json")]
    [ProducesResponseType(typeof(Achievement[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Get([FromQuery] int? userId, [FromServices] AchievementsRepository repo)
    {
        if (userId is not null)
            await repo.CheckAndAwardAchievements();
        
        var achievements = userId is null 
            ? await repo.GetAllAsync() 
            : await repo.GetByUserIdAsync(userId.Value);
        
        if (achievements is null) return NotFound();
        return Ok(achievements);
    }

    /// <summary>
    /// Gets the PNG image for an achievement.
    /// </summary>
    /// <param name="achievementId">Achievement identifier.</param>
    /// <param name="repo">Repository used to read the achievement image.</param>
    /// <returns>The achievement image file.</returns>
    [HttpGet("{achievementId:int}/photo")]
    [Produces("image/png")]
    [ProducesResponseType(typeof(FileContentResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPhotoAsync([FromRoute] int achievementId, [FromServices] AchievementsRepository repo)
    {
        byte[]? content = await repo.GetPhotoAsync(achievementId);
        if (content is null) return NotFound();
        return File(content, "image/png");
    }

    /// <summary>
    /// Creates an achievement with localized content and an image.
    /// </summary>
    /// <param name="sql">SQL query used to determine which users receive the achievement.</param>
    /// <param name="contents">JSON array of localized achievement content entries.</param>
    /// <param name="file">Achievement image file.</param>
    /// <param name="jwtService">JWT validation service.</param>
    /// <param name="usersRepo">Repository used to verify administrator access.</param>
    /// <param name="achievementsRepo">Repository used to create the achievement.</param>
    /// <returns>An HTTP result indicating whether the achievement was created.</returns>
    [HttpPost]
    [RequestSizeLimit(int.MaxValue)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Post(
        [FromForm] string sql,
        [FromForm] string contents,
        IFormFile file,
        [FromServices] JwtService jwtService, 
        [FromServices] UsersRepository usersRepo, 
        [FromServices] AchievementsRepository achievementsRepo)
    {
        string? jwt = this.GetJwt();
        if (jwt is null) 
            return BadRequest();
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized();

        PostAchievementContentRequest[]? contentsArray;
        try 
        {
            contentsArray = JsonSerializer.Deserialize<PostAchievementContentRequest[]>(contents);
            if (contentsArray is null || contentsArray.Length == 0)
                return BadRequest("Contents is required");
        }
        catch (JsonException ex)
        {
            return BadRequest($"Invalid Contents format: {ex.Message}");
        }
        
        var req = new PostAchievementRequest 
        { 
            Sql = sql, 
            Contents = contentsArray 
        };
        
        Console.WriteLine(JsonSerializer.Serialize(req));

        bool created = await achievementsRepo.CreateAchievementAsync(req, file);
        
        if (!created) return Conflict();
        return Ok();
    }
}
