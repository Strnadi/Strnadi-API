using Microsoft.AspNetCore.Mvc;
using Shared.Communication;
using Shared.Extensions;

namespace RecordingsService.Controllers;

[ApiController]
[Route("recordings/filtered/")]
public class FilteredSubrecordingsController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetFilteredSubrecordings([FromServices] RecordingsControllerClient client)
    {
        var result = await client.GetFilteredSubrecordingsAsync();

        if (result?.Value is null)
            return await this.HandleErrorResponseAsync(result?.Message);

        return Ok(result.Value);
    }
}