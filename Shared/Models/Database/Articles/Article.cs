using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.Database.Articles;

public class Article
{
    public int Id { get; set; }
    
    public string Name { get; set; }
    
    public string Description { get; set; }
    
    [NotMapped]
    public ArticleAttachment[] Files { get; set; }
    
    [NotMapped]
    public ArticleCategory[] Categories { get; set; }
}