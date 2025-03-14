using Microsoft.AspNetCore.Mvc;

namespace Utils;

[ApiController]
[Route("utils")]
public class UtilsController : ControllerBase
{
    [HttpHead("health")]
    public IActionResult Health() => Ok();
}