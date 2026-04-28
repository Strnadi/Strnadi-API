namespace Shared.Models.Database.Articles;

public class ArticleTranslation
{
    public int Id { get; set; }
    
    public int ArticleId { get; set; }
    
    public string LanguageCode { get; set; }
    
    public string Value { get; set; }
}