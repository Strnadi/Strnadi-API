namespace Tenant.Domain.Entities;

public partial class Achievement
{
    public int Id { get; set; }

    public string? ImagePath { get; set; }

    public string Sql { get; set; } = null!;

    public virtual ICollection<AchievementContent> AchievementContents { get; set; } = new List<AchievementContent>();

    public virtual ICollection<UserAchievement> UserAchievements { get; set; } = new List<UserAchievement>();
}
