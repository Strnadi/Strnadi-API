using Administration.Application.Projects;
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
        var projects = await db.Projects
            .Select(p => new ProjectResponse(p.Id, p.Name, p.Description, p.Domain))
            .ToListAsync();

        return Ok(projects);
    }
}
