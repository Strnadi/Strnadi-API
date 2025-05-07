namespace Shared.Models.Database.Articles;

public class ArticleAttachmentModel
{
    public int Id { get; set; }

    public int ArticleId { get; set; }

    public string FileName { get; set; }
}