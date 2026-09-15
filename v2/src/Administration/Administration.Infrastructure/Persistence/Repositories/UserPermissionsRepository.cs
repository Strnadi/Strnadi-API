using Administration.Domain.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Administration.Infrastructure.Persistence.Repositories;

public class UserPermissionsRepository(AdminDbContext db) : IUserPermissionsRepository
{
    public async Task<bool> HasPermissionAsync(Guid userId, string permission)
    {
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

        return await db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(db.Roles.Where(r => !blockedProjectIds.Contains(r.ProjectId)), ur => ur.RoleId, r => r.Id, (_, r) => r.Id)
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
