using Dapper;
using Microsoft.Extensions.Configuration;
using Shared.Models.Database.Achievements;
using Shared.Tools;

namespace Repository;

public class AchievementsRepository : RepositoryBase
{
    private LinkGenerator _linkGenerator;
    
    public AchievementsRepository(IConfiguration configuration) : base(configuration)
    {
        _linkGenerator = new LinkGenerator(configuration);
    }

    public async Task<Achievement[]?> GetAllAsync()
    {
        
        var achievements = await ExecuteSafelyAsync(Connection.QueryAsync<Achievement>(
            "SELECT * FROM achievements")) ;
        if (achievements is null) return null;
        
        var arr = achievements.ToArray();

        foreach (var achievement in arr)
            achievement.ImageUrl = _linkGenerator.GenerateAchievementImageUrl(achievement.Id);
             
        return arr;
    }

    public async Task<Achievement?> GetByIdAsync(int achievementId) =>
        await ExecuteSafelyAsync(Connection.QueryFirstOrDefaultAsync(
            "SELECT * FROM achievements WHERE id = @Id", new { Id = achievementId }));
    

    public async Task<byte[]?> GetPhotoAsync(int achievementId)
    {
        var achievement = await GetByIdAsync(achievementId);
        if (achievement is null) return null;
        return await File.ReadAllBytesAsync(achievement.ImagePath);
    }
}