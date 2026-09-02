namespace Strnadi.Domain.Entities;

public partial class ArticleTranslation
{
    public int Id { get; set; }

    public int ArticleId { get; set; }

    public string LanguageCode { get; set; } = null!;

    public string NameValue { get; set; } = null!;

    public string DescriptionValue { get; set; } = null!;

    public virtual Article Article { get; set; } = null!;
}
