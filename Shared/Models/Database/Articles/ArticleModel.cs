using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.Database.Articles;

public class ArticleModel
{
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public string Description { get; set; }
    
    [NotMapped]
    public ArticleAttachmentModel[] Files { get; set; }
    
    [NotMapped]
    public ArticleCategoryModel[] Categories { get; set; }
}