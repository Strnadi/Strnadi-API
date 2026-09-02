namespace Tenant.Domain.Entities;

public partial class ArticleCategory
{
    public int Id { get; set; }

    public string? Label { get; set; }

    public string Name { get; set; } = null!;

    public int? Order { get; set; }

    public virtual ICollection<ArticleCategoryAssignment> ArticleCategoryAssignments { get; set; } = new List<ArticleCategoryAssignment>();

    public virtual ICollection<ArticleCategoryTranslation> ArticleCategoryTranslations { get; set; } = new List<ArticleCategoryTranslation>();
}