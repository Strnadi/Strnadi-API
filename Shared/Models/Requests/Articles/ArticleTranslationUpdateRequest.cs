using System.ComponentModel.DataAnnotations.Schema;

namespace Shared.Models.Requests.Articles;

public class ArticleTranslationUpdateRequest
{
    [Column("language_code")]
    public string? LanguageCode { get; set; }

    [Column("name_value")]
    public string? NameValue { get; set; }

    [Column("description_value")]
    public string? DescriptionValue { get; set; }
}
