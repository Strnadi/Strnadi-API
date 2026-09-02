namespace Tenant.Application.Articles;

public record ArticleCategoryTranslationUpdateRequest(string? LanguageCode, string? NameValue, string? DescriptionValue);
