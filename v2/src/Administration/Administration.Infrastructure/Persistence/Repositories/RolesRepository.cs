using Administration.Domain.Entities;
using Administration.Domain.Exceptions;
using Administration.Domain.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace Administration.Infrastructure.Persistence.Repositories;

public class RolesRepository(AdminDbContext db) : IRolesRepository
{
    public async Task<Role[]> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Roles.Include(r => r.Permissions).ToArrayAsync(cancellationToken);
    }

    public async Task<Role?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<Permission[]> GetAllPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return await db.Permissions.ToArrayAsync(cancellationToken);
    }

    public void Add(Role role)
    {
        db.Roles.Add(role);
    }

    public async Task AssignPermissionAsync(int roleId, int permissionId, CancellationToken cancellationToken = default)
    {
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), roleId);
        var permission = await db.Permissions.FirstOrDefaultAsync(p => p.Id == permissionId, cancellationToken)
            ?? throw new NotFoundException(nameof(Permission), permissionId);

        if (role.Permissions.All(p => p.Id != permission.Id))
            role.Permissions.Add(permission);
    }

    public async Task AssignToUserAsync(int userId, int roleId, CancellationToken cancellationToken = default)
    {
        var user = await db.Users.Include(u => u.Roles).FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            ?? throw new NotFoundException(nameof(User), userId);
        var role = await db.Roles.FirstOrDefaultAsync(r => r.Id == roleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Role), roleId);

        if (user.Roles.All(r => r.Id != role.Id))
            user.Roles.Add(role);
    }
}