using System.Security.Claims;
using Administration.Application.Projects;
using Administration.Application.Users;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Controllers;

[ApiController]
[Route("users")]
[Authorize(Policy = "AccountMutation")]
public class UsersController(AdminDbContext db, IUserPermissionsRepository permissions) : ControllerBase
{
    /// <summary>Lists users, with fields limited to what the caller's permissions allow.</summary>
    [HttpGet]
    public async Task<IActionResult> GetUsersAsync()
    {
        var visibility = await GetVisibilityAsync();
        if (visibility == UserVisibility.None)
            return Forbid();

        var query = db.Users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName);

        if (visibility == UserVisibility.Confidential)
        {
            var users = await query
                .Select(u => new UserConfidentialResponse(
                    u.Id, u.FirstName, u.LastName, u.CreatedAt, u.Deleted, u.Legacy, u.EmailConfirmed, u.Email, u.City, u.PostCode))
                .ToListAsync();
            return Ok(users);
        }
        else
        {
            var users = await query
                .Select(u => new UserBasicResponse(u.Id, u.FirstName, u.LastName, u.CreatedAt, u.Deleted, u.Legacy, u.EmailConfirmed))
                .ToListAsync();
            return Ok(users);
        }
    }

    /// <summary>Gets a single user, with fields limited to what the caller's permissions allow.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetUserAsync(Guid id)
    {
        var visibility = await GetVisibilityAsync();
        if (visibility == UserVisibility.None)
            return Forbid();

        if (visibility == UserVisibility.Confidential)
        {
            var user = await db.Users
                .Where(u => u.Id == id)
                .Select(u => new UserConfidentialResponse(
                    u.Id, u.FirstName, u.LastName, u.CreatedAt, u.Deleted, u.Legacy, u.EmailConfirmed, u.Email, u.City, u.PostCode))
                .FirstOrDefaultAsync();

            return user is null ? NotFound() : Ok(user);
        }
        else
        {
            var user = await db.Users
                .Where(u => u.Id == id)
                .Select(u => new UserBasicResponse(u.Id, u.FirstName, u.LastName, u.CreatedAt, u.Deleted, u.Legacy, u.EmailConfirmed))
                .FirstOrDefaultAsync();

            return user is null ? NotFound() : Ok(user);
        }
    }

    /// <summary>Projects the given user is a member of. Anyone can look up their own; looking up
    /// someone else's requires the same visibility as <see cref="GetUserAsync"/>.</summary>
    [HttpGet("{id:guid}/projects")]
    public async Task<IActionResult> GetUserProjectsAsync(Guid id)
    {
        if (id != GetCurrentUserId())
        {
            var visibility = await GetVisibilityAsync();
            if (visibility == UserVisibility.None)
                return Forbid();
        }

        // Membership, not role, is the actual "is this user in this project" source of truth -
        // ProjectMembersController.JoinAsync always writes a ProjectMembership row on join, but
        // only adds a UserRoles row when a RoleId was given. Querying UserRoles here would miss
        // anyone who joined without being assigned a role yet.
        var projects = await db.ProjectMemberships
            .Where(pm => pm.UserId == id)
            .Join(db.Projects, pm => pm.ProjectId, p => p.Id,
                (_, p) => new ProjectResponse(p.Id, p.Name, p.Description, p.Domain, p.ApiDomain))
            .ToListAsync();

        return Ok(projects);
    }

    private enum UserVisibility { None, Basic, Confidential }

    private async Task<UserVisibility> GetVisibilityAsync()
    {
        var callerId = GetCurrentUserId();

        var canManageUsers = await permissions.HasPermissionAsync(callerId, Permissions.ManageUsers);
        if (canManageUsers || await permissions.HasPermissionAsync(callerId, Permissions.ViewUsersConfidential))
            return UserVisibility.Confidential;

        if (await permissions.HasPermissionAsync(callerId, Permissions.ViewUsersBasic))
            return UserVisibility.Basic;

        return UserVisibility.None;
    }

    private Guid GetCurrentUserId()
    {
        var userId = User.FindFirstValue(OpenIddictConstants.Claims.Subject) ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.Parse(userId!);
    }
}
