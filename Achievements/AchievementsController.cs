using Microsoft.AspNetCore.Mvc;
using Repository;

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
}