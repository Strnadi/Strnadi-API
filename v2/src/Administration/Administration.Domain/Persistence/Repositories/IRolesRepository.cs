using Administration.Domain.Entities;

namespace Administration.Domain.Persistence.Repositories;

// Minimal surface to compose roles out of the fixed Permission catalog and assign them to
// users; no REST/UI sits on top of this yet, it exists so the auth path can resolve a user's
// permission set and so roles can be seeded/assigned manually until that UI exists.
public interface IRolesRepository
{
    Task<Role[]> GetAllAsync(CancellationToken cancellationToken = default);

    // Eager-loads Permissions.
    Task<Role?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Permission[]> GetAllPermissionsAsync(CancellationToken cancellationToken = default);

    void Add(Role role);

    Task AssignPermissionAsync(int roleId, int permissionId, CancellationToken cancellationToken = default);

    Task AssignToUserAsync(int userId, int roleId, CancellationToken cancellationToken = default);
}