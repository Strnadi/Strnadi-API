namespace Tenant.Domain.Entities;

public partial class UserAchievement
{
    public int Id { get; set; }

    public Guid UserId { get; set; }

    public int AchievementId { get; set; }

    public virtual Achievement Achievement { get; set; } = null!;
}
