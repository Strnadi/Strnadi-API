namespace Shared.Models.Requests.Achievements;

public struct PostAchievementRequest
{
    public string Sql { get; set; }
    
    public PostAchievementContentRequest[] Contents { get; set; }
}