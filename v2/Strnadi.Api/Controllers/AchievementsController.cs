using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("achievements")]
public class AchievementsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] int? userId, CancellationToken cancellationToken)
    {
    }

    [HttpGet("{achievementId:int}/photo")]
    public async Task<IActionResult> GetPhotoAsync([FromRoute] int achievementId, CancellationToken cancellationToken)
    {
    }

    // TODO: old design stores an admin-supplied raw SQL condition (`sql`) and executes it directly to decide
    // who earns the achievement — flagged as a critical security issue in backend-review.md (arbitrary SQL
    // execution). Needs a redesign (fixed criteria enum or a safe rule DSL) before this is implemented for
    // real, not ported as-is.
    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    [RequestSizeLimit(int.MaxValue)]
    public async Task<IActionResult> CreateAsync([FromForm] string sql, [FromForm] string contents, IFormFile file, CancellationToken cancellationToken)
    {
    }
}
