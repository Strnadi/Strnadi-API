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
}