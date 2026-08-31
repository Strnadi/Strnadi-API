using System.Text.Json;
using Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository;
using Shared.Extensions;
using Shared.Models.Requests.Achievements;

namespace Achievements;

[ApiController]
[Route("achievements")]
public class AchievementsController : ControllerBase
{
    /// <summary>
    /// Lists achievements. When <paramref name="userId"/> is provided, evaluates and returns that
    /// user's achievement progress; otherwise returns all achievement definitions.
    /// </summary>
    /// <param name="userId">Identifier of the user to get achievement progress for. Omit to list all achievement definitions.</param>
    /// <returns>The achievements, or 404 if none could be loaded.</returns>
    [HttpGet]
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
    /// Downloads the icon image for an achievement.
    /// </summary>
    /// <param name="achievementId">Identifier of the achievement to get the icon for.</param>
    /// <returns>The icon as a PNG image, or 404 if it does not exist.</returns>
    [HttpGet("{achievementId:int}/photo")]
    public async Task<IActionResult> GetPhotoAsync([FromRoute] int achievementId, [FromServices] AchievementsRepository repo)
    {
        byte[]? content = await repo.GetPhotoAsync(achievementId);
        if (content is null) return NotFound();
        return File(content, "image/png");
    }

    /// <summary>
    /// Creates a new achievement definition with its icon. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="sql">SQL expression used to evaluate whether a user has earned the achievement.</param>
    /// <param name="contents">JSON-encoded array of localized achievement content (title/description per language).</param>
    /// <param name="file">The achievement icon image file.</param>
    /// <returns>200 on success, 400 if the JWT is missing or <paramref name="contents"/> is invalid, 401 if the caller is not an administrator, or 409 on failure.</returns>
    [HttpPost]
    [RequestSizeLimit(int.MaxValue)]
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
