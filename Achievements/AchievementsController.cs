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

    [HttpGet("{achievementId:int}/photo")]
    public async Task<IActionResult> GetPhotoAsync([FromRoute] int achievementId, [FromServices] AchievementsRepository repo)
    {
        byte[]? content = await repo.GetPhotoAsync(achievementId);
        if (content is null) return NotFound();
        return File(content, "image/png");
    }

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