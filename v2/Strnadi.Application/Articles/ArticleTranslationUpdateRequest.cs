namespace Strnadi.Application.Articles;

public record ArticleTranslationUpdateRequest(string? LanguageCode, string? NameValue, string? DescriptionValue);
