using Microsoft.EntityFrameworkCore;
using Strnadi.Domain.Entities;
using Strnadi.Domain.Persistence.Repositories;

namespace Strnadi.Infrastructure.Persistence.Repositories;

public class AchievementsRepository(AppDbContext db) : IAchievementsRepository
{
    public Task<Achievement[]> GetAllAsync(CancellationToken cancellationToken = default) =>
        db.Achievements.ToArrayAsync(cancellationToken);

    public Task<Achievement?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Achievements.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<UserAchievement[]> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default) =>
        db.UserAchievements.Where(ua => ua.UserId == userId).ToArrayAsync(cancellationToken);

    public void Add(Achievement achievement) => db.Achievements.Add(achievement);
}
