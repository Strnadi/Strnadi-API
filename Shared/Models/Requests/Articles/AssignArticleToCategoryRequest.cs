namespace Shared.Models.Requests.Articles;

public class AssignArticleToCategoryRequest
{
    public int ArticleId { get; set; }
    
    public int Order { get; set; }
}