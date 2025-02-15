using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Controllers;

[ApiController]
[Route("utils/")]
public class UtilitiesController : ControllerBase
{
    [HttpGet("health")]
    public IActionResult Health() => Ok();
}