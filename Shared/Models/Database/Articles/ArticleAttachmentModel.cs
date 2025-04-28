namespace Shared.Models.Database.Articles;

public class ArticleAttachmentModel
{
    public int Id { get; set; }

    /// <summary>
    /// UUID, foreign key on ArticleModel
    /// </summary>
    public int ArticleId { get; set; }

    public string FileName { get; set; }
}