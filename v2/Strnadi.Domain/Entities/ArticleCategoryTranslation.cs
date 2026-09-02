namespace Strnadi.Domain.Entities;

public partial class ArticleCategoryTranslation
{
    public int Id { get; set; }

    public int ArticleCategoryId { get; set; }

    public string LanguageCode { get; set; } = null!;

    public string NameValue { get; set; } = null!;

    public string DescriptionValue { get; set; } = null!;

    public virtual ArticleCategory ArticleCategory { get; set; } = null!;
}
