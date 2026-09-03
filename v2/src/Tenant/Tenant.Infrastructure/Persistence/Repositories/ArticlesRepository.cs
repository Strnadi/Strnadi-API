using Microsoft.EntityFrameworkCore;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Infrastructure.Persistence.Repositories;

public class ArticlesRepository(TenantDbContext db) : IArticlesRepository
{
    public Task<Article[]> GetAllAsync(CancellationToken cancellationToken = default) =>
        db.Articles.ToArrayAsync(cancellationToken);

    public Task<Article?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        db.Articles.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    public Task<Article[]> GetByCategoryNameAsync(string categoryName, CancellationToken cancellationToken = default) =>
        db.Articles
            .Where(a => a.ArticleCategoryAssignments.Any(x => x.Category.Name == categoryName))
            .ToArrayAsync(cancellationToken);

    public void Add(Article article) => db.Articles.Add(article);

    public void Remove(Article article) => db.Articles.Remove(article);

    public Task<ArticleAttachment?> GetAttachmentAsync(int articleId, string fileName, CancellationToken cancellationToken = default) =>
        db.ArticleAttachments.FirstOrDefaultAsync(a => a.ArticleId == articleId && a.FileName == fileName, cancellationToken);

    public void AddAttachment(ArticleAttachment attachment) => db.ArticleAttachments.Add(attachment);

    public void RemoveAttachment(ArticleAttachment attachment) => db.ArticleAttachments.Remove(attachment);

    public Task<ArticleTranslation?> GetTranslationAsync(int id, CancellationToken cancellationToken = default) =>
        db.ArticleTranslations.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public void RemoveTranslation(ArticleTranslation translation) => db.ArticleTranslations.Remove(translation);
}
