namespace Tenant.Application.Achievements;

public record AchievementResponse(int Id, string ImageUrl, IReadOnlyList<AchievementContentResponse> Contents);

public record AchievementContentResponse(string Title, string Description, string LanguageCode);
