namespace Shared.Models.Database.Articles;

public class ArticleAttachment
{
    public int Id { get; set; }

    public int ArticleId { get; set; }

    public string FileName { get; set; }
}