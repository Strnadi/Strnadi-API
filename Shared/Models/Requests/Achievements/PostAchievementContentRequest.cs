namespace Shared.Models.Requests.Achievements;

public class PostAchievementContentRequest
{
    public string LanguageCode { get; set; }
    
    public string Title { get; set; }
    
    public string Description { get; set; }
}