using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Recordings;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("recordings")]
public class RecordingPartsController : ControllerBase
{
    [Obsolete("use part/{partId:int}/sound GET instead")]
    [HttpGet("part/{recId:int}/{partId:int}/sound")]
    public async Task<IActionResult> GetSoundLegacyAsync([FromRoute] int recId, [FromRoute] int partId, CancellationToken cancellationToken)
    {
    }

    [HttpGet("part/{partId:int}/sound")]
    public async Task<IActionResult> GetSoundAsync([FromRoute] int partId, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpPost("part")]
    [RequestSizeLimit(int.MaxValue)]
    public async Task<IActionResult> UploadPartAsync([FromBody] RecordingPartUploadRequest request, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpPost("part-new")]
    [RequestSizeLimit(int.MaxValue)]
    public async Task<IActionResult> UploadPartWithFileAsync([FromForm] RecordingPartUploadRequest request, IFormFile file, CancellationToken cancellationToken)
    {
    }
}
