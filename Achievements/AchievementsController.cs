using System.Text.Json;
using Auth.Services;
using Google.Apis.Util;
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
    public async Task<ActionResult> Get([FromServices] AchievementsRepository repo)
    {
        var achievements = await repo.GetAllAsync();
        if (achievements is null) return NotFound();
        return Ok(achievements);
    }

    [HttpGet("{achievementId:int}/photo")]
    public async Task<IActionResult> GetPhotoAsync([FromServices] AchievementsRepository repo, int achievementId)
    {
        byte[]? content = await repo.GetPhotoAsync(achievementId);
        if (content is null) return NotFound();
        return File(content, "image/png");
    }

    [HttpPost]
    [RequestSizeLimit(int.MaxValue)]
    public async Task<IActionResult> Post([FromForm] PostAchievementRequest req,
        IFormFile file,
        [FromServices] JwtService jwtService, 
        [FromServices] UsersRepository usersRepo, 
        [FromServices] AchievementsRepository achievementsRepo)
    {
        string? jwt = this.GetJwt();
        Console.WriteLine(JsonSerializer.Serialize(req));
        if (jwt is null) 
            return BadRequest();
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized();

        bool created = await achievementsRepo.CreateAchievementAsync(req, file);
        
        if (!created) return Conflict();

        return Ok();
    }
}