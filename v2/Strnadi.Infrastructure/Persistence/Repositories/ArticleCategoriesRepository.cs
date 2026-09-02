using Microsoft.EntityFrameworkCore;
using Strnadi.Domain.Entities;
using Strnadi.Domain.Persistence.Repositories;

namespace Strnadi.Infrastructure.Persistence.Repositories;

public class ArticleCategoriesRepository(AppDbContext db) : IArticleCategoriesRepository
{
    public Task<ArticleCategory[]> GetAllAsync(bool includeArticles, CancellationToken cancellationToken = default)
    {
        IQueryable<ArticleCategory> query = db.ArticleCategories;

        if (includeArticles)
            query = query.Include(c => c.ArticleCategoryAssignments).ThenInclude(a => a.Article);

        return query.ToArrayAsync(cancellationToken);
    }

    public Task<ArticleCategory?> GetByNameAsync(string name, CancellationToken cancellationToken = default) =>
        db.ArticleCategories.FirstOrDefaultAsync(c => c.Name == name, cancellationToken);

    public void Add(ArticleCategory category) => db.ArticleCategories.Add(category);

    public void Remove(ArticleCategory category) => db.ArticleCategories.Remove(category);

    public Task<ArticleCategoryTranslation?> GetTranslationAsync(int id, CancellationToken cancellationToken = default) =>
        db.ArticleCategoryTranslations.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public void RemoveTranslation(ArticleCategoryTranslation translation) => db.ArticleCategoryTranslations.Remove(translation);

    public Task<ArticleCategoryAssignment?> GetAssignmentAsync(int articleId, int categoryId, CancellationToken cancellationToken = default) =>
        db.ArticleCategoryAssignments.FirstOrDefaultAsync(a => a.ArticleId == articleId && a.CategoryId == categoryId, cancellationToken);

    public void AddAssignment(ArticleCategoryAssignment assignment) => db.ArticleCategoryAssignments.Add(assignment);

    public void RemoveAssignment(ArticleCategoryAssignment assignment) => db.ArticleCategoryAssignments.Remove(assignment);
}
