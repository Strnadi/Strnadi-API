using Repository;
using Microsoft.AspNetCore.Mvc;

namespace Recordings;

[ApiController]
[Route("recordings")]
public class RecordingsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRecordings([FromServices] RecordingsRepository repo,
        [FromQuery] string? email = null,
        [FromQuery] bool parts = false,
        [FromQuery] bool sound = false)
    {
        var recordings = await repo.Get(email, parts, sound);
        return Ok(recordings);
    }
}