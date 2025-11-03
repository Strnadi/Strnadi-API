using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Shared.Models.Database.Achievements;
using Shared.Models.Requests.Achievements;
using Shared.Tools;

namespace Repository;

public class AchievementsRepository : RepositoryBase
{
    private LinkGenerator _linkGenerator;
    
    public AchievementsRepository(IConfiguration configuration) : base(configuration)
    {
        _linkGenerator = new LinkGenerator(configuration);
    }

    private async Task<AchievementContent[]?> GetAchievementContentsAsync(int achievementId) =>
        (await ExecuteSafelyAsync(Connection.QueryAsync<AchievementContent>(
            "SELECT * FROM achievement_content WHERE achievement_id = @Id",
            new { Id = achievementId })))?.ToArray();

    public async Task<Achievement[]?> GetAllAsync()
    {
        
        var achievements = await ExecuteSafelyAsync(Connection.QueryAsync<Achievement>(
            "SELECT * FROM achievements"));
        if (achievements is null) return null;
        
        var arr = achievements.ToArray();

        foreach (var achievement in arr)
        {
            achievement.ImageUrl = _linkGenerator.GenerateAchievementImageUrl(achievement.Id);
            achievement.Contents = (await GetAchievementContentsAsync(achievement.Id))!;
        }
             
        return arr;
    }

    public async Task<Achievement?> GetByIdAsync(int achievementId) =>
        await ExecuteSafelyAsync(Connection.QueryFirstOrDefaultAsync<Achievement>(
            "SELECT * FROM achievements WHERE id = @Id", new { Id = achievementId }));

    public async Task<byte[]?> GetPhotoAsync(int achievementId)
    {
        var achievement = await GetByIdAsync(achievementId);
        if (achievement is null) return null;
        return await File.ReadAllBytesAsync(achievement.ImagePath);
    }
    
    private async Task<int?> InsertAchievementAsync(string sql) =>
        await ExecuteSafelyAsync(
            Connection.ExecuteScalarAsync<int>(
                "INSERT INTO achievements (sql) VALUES (@Sql) RETURNING id", new { Sql = sql }));
    
    private async Task UpdatePathAsync(int id, string path) =>
        await ExecuteSafelyAsync(
            Connection.ExecuteAsync(
                "UPDATE achievements SET image_path = @Path WHERE id = @Id", new { Id = id, Path = path }));

    private async Task InsertAchievementContentAsync(int achievementId, params PostAchievementContentRequest[] contents) =>
        await ExecuteSafelyAsync(async () =>
        {
            await using var tx = await Connection.BeginTransactionAsync();

            foreach (var content in contents)
            {
                await Connection.ExecuteAsync(
                    """
                    INSERT INTO achievement_content (title, description, language_code, achievement_id)
                    VALUES (@Title, @Description, @LanguageCode, @AchievementId)
                    """,
                    new
                    {
                        content.Title,
                        content.Description,
                        content.LanguageCode,
                        AchievementId = achievementId
                    },
                    transaction: tx);
            }

            await tx.CommitAsync();
        });

    public async Task<bool> CreateAchievementAsync(PostAchievementRequest req, IFormFile file)
    {
        var insertedId = await InsertAchievementAsync(req.Sql);
        if (insertedId is null) return false;

        MemoryStream stream = new();
        await file.CopyToAsync(stream);
        
        string path = await FileSystemHelper.SaveAchievementImageAsync(insertedId.Value, stream.ToArray());
        await UpdatePathAsync(insertedId.Value, path);

        await InsertAchievementContentAsync(insertedId.Value, req.Contents);
        return true;
    }
}