namespace Tenant.Application.Articles;

public record ArticleSummaryResponse(int Id, string Name, string? Description);

public record ArticleCategoryResponse(int Id, string? Label, string Name, int? Order, ArticleSummaryResponse[]? Articles);
