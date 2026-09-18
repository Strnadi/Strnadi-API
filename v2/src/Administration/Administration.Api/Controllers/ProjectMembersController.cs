using System.Security.Claims;
using Administration.Application.Projects;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Controllers;

[ApiController]
[Route("projects/{projectId:guid}/members")]
[Authorize(Policy = "AccountMutation")]
public class ProjectMembersController(AdminDbContext db, UserManager<User> users, IUserPermissionsRepository permissions)
    : ControllerBase
{
    /// <summary>Lists a project's members and the roles each one holds in that project.</summary>
    [HttpGet]
    public async Task<IActionResult> GetMembersAsync(Guid projectId)
    {
        if (!await permissions.HasPermissionAsync(GetCurrentUserId(), Permissions.ManageRoles))
            return Forbid();

        if (!await db.Projects.AnyAsync(p => p.Id == projectId))
            return NotFound();

        var members = await db.ProjectMemberships
            .Where(pm => pm.ProjectId == projectId)
            .Join(db.Users, pm => pm.UserId, u => u.Id, (_, u) => u)
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .ToListAsync();

        var roleNamesByUser = await db.UserRoles
            .Join(db.Roles.Where(r => r.ProjectId == projectId), ur => ur.RoleId, r => r.Id, (ur, r) => new { ur.UserId, r.Name })
            .ToListAsync();

        var response = members.Select(u => new ProjectMemberResponse(
            u.Id, u.Email!, u.FirstName, u.LastName,
            roleNamesByUser.Where(r => r.UserId == u.Id).Select(r => r.Name!).ToList()));

        return Ok(response);
    }

    [HttpPost]
    public async Task<IActionResult> JoinAsync(Guid projectId, [FromBody] JoinProjectRequest request)
    {
        var callerId = GetCurrentUserId();
        var hasPermission = await permissions.HasPermissionAsync(callerId, Permissions.ManageRoles);

        if (!await db.Projects.AnyAsync(p => p.Id == projectId))
            return NotFound("Project not found.");

        var targetUser = await users.FindByEmailAsync(request.Email);
        if (targetUser is null)
            return NotFound("User not found.");

        if (!hasPermission && targetUser.Id != callerId)
            return Forbid();

        if (!hasPermission && request.RoleId is not null)
            return Forbid();

        Role? role = null;
        if (request.RoleId is { } roleId)
        {
            role = await db.Roles.FirstOrDefaultAsync(r => r.Id == roleId && r.ProjectId == projectId);
            if (role is null)
                return NotFound("Role not found in this project.");

            var alreadyAssigned = await db.UserRoles.AnyAsync(ur => ur.UserId == targetUser.Id && ur.RoleId == roleId);
            if (alreadyAssigned)
                return Conflict("User already has this role.");
        }

        var hasMembership = await db.ProjectMemberships.AnyAsync(pm => pm.UserId == targetUser.Id && pm.ProjectId == projectId);
        if (!hasMembership)
        {
            db.ProjectMemberships.Add(new ProjectMembership
            {
                UserId = targetUser.Id,
                ProjectId = projectId
            });
        }

        if (role is not null)
            db.UserRoles.Add(new IdentityUserRole<Guid> { UserId = targetUser.Id, RoleId = role.Id });

        await db.SaveChangesAsync();

        var roleNames = role is null ? [] : new List<string> { role.Name! };
        var response = new ProjectMemberResponse(targetUser.Id, targetUser.Email!, targetUser.FirstName, targetUser.LastName, roleNames);

        // MvcOptions.SuppressAsyncSuffixInActionNames defaults to true, so the action is routable
        // as "GetMembers", not "GetMembersAsync" - nameof() gives the C# method name, which no
        // longer matches, and CreatedAtAction throws "No route matches the supplied values."
        return CreatedAtAction(nameof(GetMembersAsync)[..^"Async".Length], new { projectId }, response);
    }

    /// <summary>Removes a user from the project entirely - the membership row and every role
    /// they hold there.</summary>
    [HttpDelete("{userId:guid}")]
    public async Task<IActionResult> RemoveAsync(Guid projectId, Guid userId)
    {
        if (!await permissions.HasPermissionAsync(GetCurrentUserId(), Permissions.ManageRoles))
            return Forbid();

        var membership = await db.ProjectMemberships.FirstOrDefaultAsync(pm => pm.UserId == userId && pm.ProjectId == projectId);
        if (membership is null)
            return NotFound();

        var rolesInProject = await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(db.Roles.Where(r => r.ProjectId == projectId), ur => ur.RoleId, r => r.Id, (ur, _) => ur)
            .ToListAsync();

        db.UserRoles.RemoveRange(rolesInProject);
        db.ProjectMemberships.Remove(membership);
        await db.SaveChangesAsync();

        return NoContent();
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirstValue(OpenIddictConstants.Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(userId!);
    }
}
