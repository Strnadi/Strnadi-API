using System.ComponentModel.DataAnnotations.Schema;
using System.Runtime.CompilerServices;

namespace Shared.Models.Database.Articles;

public class ArticleCategory
{
    public int Id { get; set; }

    public string Label { get; set; }

    public string Name { get; set; }
    
    [NotMapped]
    public Article[] Articles { get; set; }
}