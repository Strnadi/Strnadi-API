using Tenant.Domain.Entities;
using Tenant.Domain.Exceptions;
using Tenant.Domain.Persistence;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Application.Articles;

public class ArticleCategoriesService(IArticleCategoriesRepository categories, IUnitOfWork unitOfWork)
{
    public Task<ArticleCategory[]> GetAllAsync(bool includeArticles, CancellationToken cancellationToken = default) =>
        categories.GetAllAsync(includeArticles, cancellationToken);

    public async Task<int> CreateAsync(ArticleCategoryUploadRequest request, CancellationToken cancellationToken = default)
    {
        var category = new ArticleCategory { Label = request.Label, Name = request.Name };
        categories.Add(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return category.Id;
    }

    public async Task<ArticleCategoryTranslation> GetTranslationAsync(int id, CancellationToken cancellationToken = default) =>
        await categories.GetTranslationAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(ArticleCategoryTranslation), id);

    public async Task UpdateTranslationAsync(int id, ArticleCategoryTranslationUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var translation = await categories.GetTranslationAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(ArticleCategoryTranslation), id);

        translation.LanguageCode = request.LanguageCode ?? translation.LanguageCode;
        translation.NameValue = request.NameValue ?? translation.NameValue;
        translation.DescriptionValue = request.DescriptionValue ?? translation.DescriptionValue;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTranslationAsync(int id, CancellationToken cancellationToken = default)
    {
        var translation = await categories.GetTranslationAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(ArticleCategoryTranslation), id);

        categories.RemoveTranslation(translation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(string categoryName, CancellationToken cancellationToken = default)
    {
        var category = await categories.GetByNameAsync(categoryName, cancellationToken)
            ?? throw new NotFoundException(nameof(ArticleCategory), categoryName);

        categories.Remove(category);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveAssignmentAsync(string categoryName, int articleId, CancellationToken cancellationToken = default)
    {
        var category = await categories.GetByNameAsync(categoryName, cancellationToken)
            ?? throw new NotFoundException(nameof(ArticleCategory), categoryName);

        var assignment = await categories.GetAssignmentAsync(articleId, category.Id, cancellationToken)
            ?? throw new NotFoundException(nameof(ArticleCategoryAssignment), $"{categoryName}/{articleId}");

        categories.RemoveAssignment(assignment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
