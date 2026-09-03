using Microsoft.EntityFrameworkCore;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Infrastructure.Persistence.Repositories;

public class AchievementsRepository(TenantDbContext db) : IAchievementsRepository
{
    public Task<Achievement[]> GetAllAsync(CancellationToken cancellationToken = default) =>
        db.Achievements.Include(a => a.AchievementContents).ToArrayAsync(cancellationToken);

    public Task<Achievement?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Achievements.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Achievement[]> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        db.Achievements
            .Include(a => a.AchievementContents)
            .Where(a => a.UserAchievements.Any(ua => ua.UserId == userId))
            .ToArrayAsync(cancellationToken);

    public void Add(Achievement achievement) => db.Achievements.Add(achievement);

    public Task<int[]> GetEligibleUserIdsAsync(string sql, CancellationToken cancellationToken = default) =>
        db.Database.SqlQueryRaw<int>(sql).ToArrayAsync(cancellationToken);

    public void Award(int userId, int achievementId) =>
        db.UserAchievements.Add(new UserAchievement { UserId = userId, AchievementId = achievementId });
}
