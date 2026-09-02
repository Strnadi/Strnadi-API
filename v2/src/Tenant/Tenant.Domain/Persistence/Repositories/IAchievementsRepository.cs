using Tenant.Domain.Entities;

namespace Tenant.Domain.Persistence.Repositories;

public interface IAchievementsRepository
{
    Task<Achievement[]> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Achievement?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Achievement[]> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    void Add(Achievement achievement);

    // TODO(security): sql is admin-authored and executed as raw SQL - see backend-review.md, needs a safe criteria/rule system.
    Task<int[]> GetEligibleUserIdsAsync(string sql, CancellationToken cancellationToken = default);

    void Award(int userId, int achievementId);
}
