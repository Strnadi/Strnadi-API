using Strnadi.Domain.Entities;
using Strnadi.Domain.Exceptions;
using Strnadi.Domain.Persistence;
using Strnadi.Domain.Persistence.Repositories;
using Strnadi.Domain.Services;

namespace Strnadi.Application.Achievements;

public class AchievementsService(IAchievementsRepository achievements, IFileStorage fileStorage, IUnitOfWork unitOfWork)
{
    public async Task<object> GetAllAsync(int? userId, CancellationToken cancellationToken = default)
    {
        if (userId is null)
            return await achievements.GetAllAsync(cancellationToken);

        await CheckAndAwardAchievementsAsync(userId.Value, cancellationToken);
        return await achievements.GetByUserIdAsync(userId.Value, cancellationToken);
    }

    public async Task<byte[]> GetPhotoAsync(int achievementId, CancellationToken cancellationToken = default)
    {
        var achievement = await achievements.GetByIdAsync(achievementId, cancellationToken)
            ?? throw new NotFoundException(nameof(Achievement), achievementId);

        var bytes = achievement.ImagePath is not null
            ? await fileStorage.ReadAsync(achievement.ImagePath, cancellationToken)
            : null;

        return bytes ?? throw new NotFoundException(nameof(Achievement), achievementId);
    }

    public async Task<int> CreateAsync(string sql, List<AchievementContentRequest> contents, byte[] imageContent, CancellationToken cancellationToken = default)
    {
        // TODO(security): sql is admin-supplied and executed as-is - see backend-review.md.
        var achievement = new Achievement { Sql = sql };

        foreach (var content in contents)
        {
            achievement.AchievementContents.Add(new AchievementContent
            {
                Title = content.Title,
                Description = content.Description,
                LanguageCode = content.LanguageCode,
            });
        }

        achievements.Add(achievement);
        await unitOfWork.SaveChangesAsync(cancellationToken); // need the generated Id for the image path

        var imagePath = $"achievements/{achievement.Id}/image.png";
        await fileStorage.SaveAsync(imagePath, imageContent, cancellationToken);

        achievement.ImagePath = imagePath;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return achievement.Id;
    }

    private async Task CheckAndAwardAchievementsAsync(int userId, CancellationToken cancellationToken)
    {
        var allAchievements = await achievements.GetAllAsync(cancellationToken);
        var userAchievements = await achievements.GetByUserIdAsync(userId, cancellationToken);
        var alreadyAwardedIds = userAchievements.Select(ua => ua.AchievementId).ToHashSet();

        foreach (var achievement in allAchievements.Where(a => !alreadyAwardedIds.Contains(a.Id)))
        {
            // TODO(security): executes admin-authored raw SQL directly - see backend-review.md.
            var eligibleUserIds = await achievements.GetEligibleUserIdsAsync(achievement.Sql, cancellationToken);
            if (eligibleUserIds.Contains(userId))
                achievements.Award(userId, achievement.Id);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
