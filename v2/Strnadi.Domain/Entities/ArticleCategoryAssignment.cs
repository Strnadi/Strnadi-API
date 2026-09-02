namespace Strnadi.Domain.Entities;

public partial class ArticleCategoryAssignment
{
    public int ArticleId { get; set; }

    public int CategoryId { get; set; }

    public int Order { get; set; }

    public virtual Article Article { get; set; } = null!;

    public virtual ArticleCategory Category { get; set; } = null!;
}
