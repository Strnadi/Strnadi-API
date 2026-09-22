using Administration.Domain.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Administration.Infrastructure.Persistence.Repositories;

public class UserPermissionsRepository(AdminDbContext db) : IUserPermissionsRepository
{
    public async Task<bool> HasPermissionAsync(Guid userId, string permission, Guid? projectId = null)
    {
        // A global required document blocks the user everywhere, regardless of which project (if
        // any) this particular check is scoped to - that's orthogonal to projectId below.
        if (await HasOutstandingConsentAsync(userId, projectId: null))
            return false;

        var now = DateTime.UtcNow;
        var acceptedDocumentIds = db.DocumentAcceptances
            .Where(da => da.UserId == userId && da.RevokedAt == null)
            .Select(da => da.DocumentId);

        var blockedProjectIds = await db.Documents
            .Where(d => d.ProjectId != null && d.IsActive && d.IsRequired && d.EffectiveAt <= now && !acceptedDocumentIds.Contains(d.Id))
            .Select(d => d.ProjectId!.Value)
            .ToListAsync();

        // projectId == null: platform-wide check - only a global role (ProjectId == null) counts.
        // projectId given: project-scoped check - that project's own role, or a global role, counts.
        var rolesQuery = projectId is null
            ? db.Roles.Where(r => r.ProjectId == null)
            : db.Roles.Where(r => r.ProjectId == null || r.ProjectId == projectId);

        // A global role has no single project to be blocked by; only a project-scoped role whose
        // own project has an outstanding required document is excluded here.
        rolesQuery = rolesQuery.Where(r => r.ProjectId == null || !blockedProjectIds.Contains(r.ProjectId.Value));

        return await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(rolesQuery, ur => ur.RoleId, r => r.Id, (_, r) => r.Id)
            .Join(db.RoleClaims.Where(rc => rc.ClaimType == "permission" && rc.ClaimValue == permission),
                roleId => roleId, rc => rc.RoleId, (_, _) => 1)
            .AnyAsync();
    }

    public Task<bool> HasOutstandingConsentAsync(Guid userId, Guid? projectId)
    {
        var now = DateTime.UtcNow;
        var acceptedDocumentIds = db.DocumentAcceptances
            .Where(da => da.UserId == userId && da.RevokedAt == null)
            .Select(da => da.DocumentId);

        return db.Documents
            .Where(d => d.IsActive && d.IsRequired && d.EffectiveAt <= now)
            .Where(d => d.ProjectId == null || d.ProjectId == projectId)
            .Where(d => !acceptedDocumentIds.Contains(d.Id))
            .AnyAsync();
    }
}
