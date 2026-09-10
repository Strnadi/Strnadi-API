using System.Security.Claims;
using Administration.Domain.Entities;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace Administration.Api.Pages.Dashboard;

public record ProjectSummary(Guid Id, string Name);

[Authorize]
public abstract class DashboardPageModel(UserManager<User> users, AdminDbContext db) : PageModel
{
    public User CurrentUser { get; private set; } = null!;
    public bool IsAdmin { get; private set; }
    public IReadOnlyList<ProjectSummary> Projects { get; private set; } = [];

    protected AdminDbContext Db => db;
    protected UserManager<User> Users => users;

    public override async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var user = userId is null ? null : await users.FindByIdAsync(userId);
        if (user is null)
        {
            context.Result = RedirectToPage("/Account/Login", new { returnUrl = Request.Path + Request.QueryString });
            return;
        }

        CurrentUser = user;

        var roleNames = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.Name)
            .ToListAsync();
        IsAdmin = roleNames.Contains("admin");

        Projects = await db.UserRoles
            .Where(ur => ur.UserId == user.Id)
            .Join(db.Roles, ur => ur.RoleId, r => r.Id, (_, r) => r.ProjectId)
            .Distinct()
            .Join(db.Projects, projectId => projectId, p => p.Id, (_, p) => new ProjectSummary(p.Id, p.Name))
            .ToListAsync();

        await next();
    }
}
