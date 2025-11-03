using Microsoft.AspNetCore.Mvc;

namespace Achievements;

[ApiController]
[Route("achievements")]
public class AchievementsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> PostAchievementAsync()
    {
        throw new Exception();
    }
}