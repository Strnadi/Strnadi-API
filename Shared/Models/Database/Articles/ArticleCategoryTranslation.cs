namespace Shared.Models.Database.Articles;

public class ArticleCategoryTranslation
{
    public int Id { get; set; }

    public int ArticleCategoryId { get; set; }

    public string LanguageCode { get; set; }
    
    public string Value { get; set; }
}