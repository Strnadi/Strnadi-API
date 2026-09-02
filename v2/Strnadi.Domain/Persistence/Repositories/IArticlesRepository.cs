using Strnadi.Domain.Entities;

namespace Strnadi.Domain.Persistence.Repositories;

public interface IArticlesRepository
{
    Task<Article[]> GetAllAsync(CancellationToken cancellationToken = default);

    Task<Article?> GetByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<Article[]> GetByCategoryNameAsync(string categoryName, CancellationToken cancellationToken = default);

    void Add(Article article);

    void Remove(Article article);

    Task<ArticleAttachment?> GetAttachmentAsync(int articleId, string fileName, CancellationToken cancellationToken = default);

    void AddAttachment(ArticleAttachment attachment);

    void RemoveAttachment(ArticleAttachment attachment);

    Task<ArticleTranslation?> GetTranslationAsync(int id, CancellationToken cancellationToken = default);

    void RemoveTranslation(ArticleTranslation translation);
}
