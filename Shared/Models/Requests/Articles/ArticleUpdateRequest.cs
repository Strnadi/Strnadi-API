using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.Requests.Articles;

public class ArticleUpdateRequest
{
    [Column("name")]
    public string Name { get; set; }
    
    [Column("description")]
    public string Description { get; set; }
}