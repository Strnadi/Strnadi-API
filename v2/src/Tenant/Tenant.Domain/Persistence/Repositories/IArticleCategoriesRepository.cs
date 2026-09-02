using Tenant.Domain.Entities;

namespace Tenant.Domain.Persistence.Repositories;

public interface IArticleCategoriesRepository
{
    Task<ArticleCategory[]> GetAllAsync(bool includeArticles, CancellationToken cancellationToken = default);

    Task<ArticleCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    void Add(ArticleCategory category);

    void Remove(ArticleCategory category);

    Task<ArticleCategoryTranslation?> GetTranslationAsync(int id, CancellationToken cancellationToken = default);

    void RemoveTranslation(ArticleCategoryTranslation translation);

    Task<ArticleCategoryAssignment?> GetAssignmentAsync(int articleId, int categoryId, CancellationToken cancellationToken = default);

    void AddAssignment(ArticleCategoryAssignment assignment);

    void RemoveAssignment(ArticleCategoryAssignment assignment);
}
