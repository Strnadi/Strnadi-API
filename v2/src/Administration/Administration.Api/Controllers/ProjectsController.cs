using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Administration.Api.Controllers;

[ApiController]
[Route("projects")]
public class ProjectsController(AdminDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetProjects()
    {
        return Ok(await db.Projects.ToListAsync());
    }
}