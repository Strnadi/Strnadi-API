using Administration.Domain.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Administration.Infrastructure.Persistence.Repositories;

public class UserPermissionsRepository(AdminDbContext db) : IUserPermissionsRepository
{
    public Task<bool> HasPermissionAsync(Guid userId, string permission) =>
        db.UserRoles
            .Where(ur => ur.UserId == userId)
            .Join(db.RoleClaims.Where(rc => rc.ClaimType == "permission" && rc.ClaimValue == permission),
                ur => ur.RoleId, rc => rc.RoleId, (_, _) => 1)
            .AnyAsync();
}
