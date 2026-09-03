using Administration.Domain.Entities;

namespace Administration.Domain.Persistence.Repositories;

public interface IUsersRepository
{
    Task<User[]> GetAllAsync(CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    // Eager-loads Roles.Permissions; needed to resolve the caller's permission set at login/token time.
    Task<User?> GetByIdWithRolesAsync(int id, CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default);

    Task<User?> GetByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default);

    Task<User?> GetByAppleIdAsync(string appleId, CancellationToken cancellationToken = default);

    void Add(User user);

    void Update(User user);

    void Remove(User user);
}