namespace Tenant.Domain.Entities;

public partial class AchievementContent
{
    public int Id { get; set; }

    public string Title { get; set; } = null!;

    public string Description { get; set; } = null!;

    public string LanguageCode { get; set; } = null!;

    public int AchievementId { get; set; }

    public virtual Achievement Achievement { get; set; } = null!;
}
