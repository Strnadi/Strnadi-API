using Strnadi.Domain.Entities;
using Strnadi.Domain.Exceptions;
using Strnadi.Domain.Persistence;
using Strnadi.Domain.Persistence.Repositories;
using Strnadi.Domain.Services;

namespace Strnadi.Application.Articles;

public class ArticlesService(
    IArticlesRepository articles,
    IArticleCategoriesRepository categories,
    IFileStorage fileStorage,
    IUnitOfWork unitOfWork)
{
    public Task<Article[]> GetAllAsync(CancellationToken cancellationToken = default) =>
        articles.GetAllAsync(cancellationToken);

    public Task<Article[]> GetByCategoryAsync(string categoryName, CancellationToken cancellationToken = default) =>
        articles.GetByCategoryNameAsync(categoryName, cancellationToken);

    public async Task<Article> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        await articles.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Article), id);

    public async Task<byte[]> GetAttachmentAsync(int id, string fileName, CancellationToken cancellationToken = default)
    {
        _ = await articles.GetAttachmentAsync(id, fileName, cancellationToken)
            ?? throw new NotFoundException(nameof(ArticleAttachment), fileName);

        var content = await fileStorage.ReadAsync(AttachmentPath(id, fileName), cancellationToken);
        return content ?? throw new NotFoundException(nameof(ArticleAttachment), fileName);
    }

    public async Task<int> CreateAsync(ArticleUploadRequest request, CancellationToken cancellationToken = default)
    {
        var article = new Article { Name = request.Name, Description = request.Description };
        articles.Add(article);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return article.Id;
    }

    public async Task UploadAttachmentAsync(int id, string fileName, string base64, CancellationToken cancellationToken = default)
    {
        _ = await articles.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Article), id);

        await fileStorage.SaveAsync(AttachmentPath(id, fileName), Convert.FromBase64String(base64), cancellationToken);

        articles.AddAttachment(new ArticleAttachment { ArticleId = id, FileName = fileName });
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(int id, ArticleUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var article = await articles.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Article), id);

        article.Name = request.Name ?? article.Name;
        article.Description = request.Description ?? article.Description;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<ArticleTranslation> GetTranslationAsync(int id, CancellationToken cancellationToken = default) =>
        await articles.GetTranslationAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(ArticleTranslation), id);

    public async Task UpdateTranslationAsync(int id, ArticleTranslationUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var translation = await articles.GetTranslationAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(ArticleTranslation), id);

        translation.LanguageCode = request.LanguageCode ?? translation.LanguageCode;
        translation.NameValue = request.NameValue ?? translation.NameValue;
        translation.DescriptionValue = request.DescriptionValue ?? translation.DescriptionValue;

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteTranslationAsync(int id, CancellationToken cancellationToken = default)
    {
        var translation = await articles.GetTranslationAsync(id, cancellationToken)
            ?? throw new NotFoundException(nameof(ArticleTranslation), id);

        articles.RemoveTranslation(translation);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAttachmentAsync(int id, string fileName, string base64, CancellationToken cancellationToken = default)
    {
        _ = await articles.GetAttachmentAsync(id, fileName, cancellationToken)
            ?? throw new NotFoundException(nameof(ArticleAttachment), fileName);

        await fileStorage.SaveAsync(AttachmentPath(id, fileName), Convert.FromBase64String(base64), cancellationToken);
    }

    public async Task AssignToCategoryAsync(string categoryName, AssignArticleToCategoryRequest request, CancellationToken cancellationToken = default)
    {
        var article = await articles.GetByIdAsync(request.ArticleId, cancellationToken)
            ?? throw new NotFoundException(nameof(Article), request.ArticleId);

        var category = await categories.GetByNameAsync(categoryName, cancellationToken)
            ?? throw new NotFoundException(nameof(ArticleCategory), categoryName);

        var existing = await categories.GetAssignmentAsync(article.Id, category.Id, cancellationToken);
        if (existing is not null)
        {
            existing.Order = request.Order;
        }
        else
        {
            categories.AddAssignment(new ArticleCategoryAssignment
            {
                ArticleId = article.Id,
                CategoryId = category.Id,
                Order = request.Order,
            });
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        var article = await articles.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(Article), id);
        articles.Remove(article);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAttachmentAsync(int id, string fileName, CancellationToken cancellationToken = default)
    {
        var attachment = await articles.GetAttachmentAsync(id, fileName, cancellationToken)
            ?? throw new NotFoundException(nameof(ArticleAttachment), fileName);

        fileStorage.Delete(AttachmentPath(id, fileName), cancellationToken);
        articles.RemoveAttachment(attachment);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static string AttachmentPath(int articleId, string fileName) => $"articles/{articleId}/{fileName}";
}
