namespace Shared.Models.Requests.Achievements;

public class PostAchievementRequest
{
    public string Sql { get; set; }
    
    public PostAchievementContentRequest[] Contents { get; set; }
}