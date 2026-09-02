using Strnadi.Domain.Entities;

namespace Strnadi.Domain.Persistence.Repositories;

public interface IAchievementsRepository
{
    Task<Achievement[]> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Achievement?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<UserAchievement[]> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);

    void Add(Achievement achievement);
}
