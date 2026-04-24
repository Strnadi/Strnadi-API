using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Dapper;
using Microsoft.Extensions.Configuration;
using Shared.Models.Database.Articles;
using Shared.Models.Requests.Articles;
using Shared.Tools;

namespace Repository;

/// <summary>
/// Provides data and file persistence operations for articles, article attachments, and article categories.
/// </summary>
public class ArticlesRepository : RepositoryBase
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ArticlesRepository"/> class.
    /// </summary>
    /// <param name="configuration">Application configuration used to initialize the repository connection.</param>
    public ArticlesRepository(IConfiguration configuration) : base(configuration)
    {
    }

    /// <summary>
    /// Gets all articles with their attachments and categories.
    /// </summary>
    /// <returns>An array of articles, or null when the query fails.</returns>
    public async Task<Article[]?> GetAsync()
    {
        var articles = await GetArticlesAsync();
        
        if (articles is null)
            return null;

        foreach (var article in articles)
        {
            article.Files = (await GetArticleFilesAsync(article.Id))!;
            if (article.Files == null!)
                return null;
            article.Categories = (await GetCategoriesByArticleAsync(article.Id))!;
            if (article.Categories == null!)
                return null;
        }
        
        return articles;
    }

    private async Task<Article[]?> GetArticlesAsync() =>
        await ExecuteSafelyAsync(async () =>
            (await Connection.QueryAsync<Article>("SELECT * FROM articles")).ToArray());

    private async Task<Article[]?> GetArticlesByCategoryAsync(int categoryId)
    {
        var assignments = await GetCategoryAssignmentsByCategoryAsync(categoryId);
        if (assignments is null)
            return null;

        var articles = new Article[assignments.Length];
        for (int i = 0; i < articles.Length; i++)
        {
            var article = await GetArticleAsync(assignments[i].ArticleId);
            if (article is null)
                return null;
            articles[i] = article;
        }

        return articles;
    }
    
    private async Task<ArticleAttachment[]?> GetArticleFilesAsync(int articleId) => 
        await ExecuteSafelyAsync(async () => 
            (await Connection.QueryAsync<ArticleAttachment>("SELECT * FROM article_attachments WHERE article_id = @ArticleId",
                new
                {
                    ArticleId = articleId
                })).ToArray());

    /// <summary>
    /// Gets articles assigned to a category by category name.
    /// </summary>
    /// <param name="categoryName">The category name to query.</param>
    /// <returns>An array of articles assigned to the category, or null when the query fails.</returns>
    public async Task<Article[]?> GetAsync(string categoryName)
    {
        var assignments = await GetCategoryAssignmentsByCategoryAsync(categoryName);
        if (assignments is null)
            return null;

        var articles = new Article?[assignments.Length];
        for (int i = 0; i < articles.Length; i++)
        {
            articles[i] = await GetAsync(assignments[i].ArticleId);
            if (articles[i] is null)
                return null;
        }   
        return articles!;
    }

    private async Task<ArticleCategoryAssignment[]?> GetCategoryAssignmentsByCategoryAsync(string categoryName) =>
        await ExecuteSafelyAsync(async () =>
            (await Connection.QueryAsync<ArticleCategoryAssignment>(
                """
                SELECT * 
                FROM article_category_assignment 
                WHERE category_id = (
                    SELECT id 
                    FROM article_categories 
                    WHERE name = @CategoryName
                )
                """, new { CategoryName = categoryName })).ToArray());

    /// <summary>
    /// Gets an article by id with its attachments and categories.
    /// </summary>
    /// <param name="id">The article id.</param>
    /// <returns>The article, or null when it does not exist or the query fails.</returns>
    public async Task<Article?> GetAsync(int id)
    {
        var article = await GetArticleAsync(id);
        if (article is null)
            return null;
        
        article.Files = (await GetArticleFilesAsync(id))!;
        if (article.Files == null!)
            return null;

        article.Categories = (await GetCategoriesByArticleAsync(id))!;
        if (article.Categories == null!)
            return null;
        
        return article;
    }

    private async Task<Article?> GetArticleAsync(int id) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.QueryFirstOrDefaultAsync<Article>(
                "SELECT * FROM articles WHERE id = @Id", new { Id = id }));

    private async Task<ArticleCategory[]?> GetCategoriesByArticleAsync(int articleId)
    {
        var assignments = await GetCategoryAssignmentsByArticleAsync(articleId);
        if (assignments is null)
            return null;
        
        var categories = new ArticleCategory[assignments.Length];
        for (int i = 0; i < categories.Length; i++)
        {
            categories[i] = (await GetCategoryAsync(assignments[i].CategoryId))!;
            if (categories[i] == null!)
                return null;
        }

        return categories;
    }

    private async Task<ArticleCategoryAssignment[]?> GetCategoryAssignmentsByCategoryAsync(int categoryId) => 
        await ExecuteSafelyAsync(async () =>
            (await Connection.QueryAsync<ArticleCategoryAssignment>(
                "SELECT * FROM article_category_assignment WHERE category_id = @CategoryId ORDER BY \"order\"",
                new { CategoryId = categoryId })).ToArray());
    
    private async Task<ArticleCategoryAssignment[]?> GetCategoryAssignmentsByArticleAsync(int articleId) =>
        await ExecuteSafelyAsync(async () => 
            (await Connection.QueryAsync<ArticleCategoryAssignment>(
                "SELECT * FROM article_category_assignment WHERE article_id = @ArticleId ORDER BY \"order\"",
                new
                {
                    ArticleId = articleId
                })).ToArray());

    private async Task<ArticleCategory?> GetCategoryAsync(int categoryId) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.QueryFirstOrDefaultAsync<ArticleCategory>(
                "SELECT * FROM article_categories WHERE id = @CategoryId", 
                new { CategoryId = categoryId }));

    /// <summary>
    /// Gets an article attachment file by article id and file name.
    /// </summary>
    /// <param name="id">The article id.</param>
    /// <param name="fileName">The attachment file name.</param>
    /// <returns>The attachment bytes, or null when the file does not exist.</returns>
    public async Task<byte[]?> GetAsync(int id, string fileName)
    {
        if (!FileSystemHelper.ArticleFileExists(id, fileName))
            return null;
        
        byte[] content = await FileSystemHelper.ReadArticleFileAsync(id, fileName);
        return content;
    }

    /// <summary>
    /// Saves a new article.
    /// </summary>
    /// <param name="req">The article data to save.</param>
    /// <returns>The created article id, or null when the insert fails.</returns>
    public async Task<int?> SaveArticleAsync(ArticleUploadRequest req) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteScalarAsync<int?>(
                """
                INSERT INTO articles(name, description)
                VALUES (@Name, @Description)
                RETURNING id
                """, new { req.Name, req.Description }));

    /// <summary>
    /// Saves an article attachment file and records it in the database.
    /// </summary>
    /// <param name="articleId">The article id.</param>
    /// <param name="fileName">The attachment file name.</param>
    /// <param name="base64">The attachment content encoded as Base64.</param>
    /// <returns>True when the database record is inserted; otherwise, false.</returns>
    public async Task<bool> SaveArticleAttachmentAsync(int articleId, string fileName, string base64)
    {
        await FileSystemHelper.SaveArticleFileAsync(articleId, fileName, base64);
        return await InsertAttachmentAsync(articleId, fileName);
    }

    private async Task<bool> InsertAttachmentAsync(int articleId, string fileName) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteAsync(
                """
                INSERT INTO article_attachments(article_id, file_name)
                VALUES (@ArticleId, @FileName)
                """, new { ArticleId = articleId, FileName = fileName })) != 0;

    private async Task<bool> ExistsAsync(int id) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteScalarAsync<int>("SELECT COUNT(*) FROM articles WHERE id = @Id", new { Id = id }) != 0);

    /// <summary>
    /// Updates an existing article.
    /// </summary>
    /// <param name="id">The article id.</param>
    /// <param name="req">The article fields to update.</param>
    /// <returns>True when the article exists and is updated; otherwise, false.</returns>
    public async Task<bool> UpdateArticleAsync(int id, ArticleUpdateRequest req)
    {
        if (!await ExistsAsync(id))
            return false;

        return await ExecuteSafelyAsync(async () =>
        {

            var updateFields = new List<string>();
            var parameters = new DynamicParameters();
            
            parameters.Add("Id", id); 
            
            foreach (var prop in req.GetType().GetProperties()
                         .Where(p => p.GetCustomAttribute<ColumnAttribute>() is not null))
            {
                string columnName = prop.GetCustomAttribute<ColumnAttribute>()!.Name!;
                updateFields.Add($"{columnName} = @{prop.Name}");
                parameters.Add(prop.Name, prop.GetValue(req));
            }

            var sql = $"UPDATE articles SET {string.Join(", ", updateFields)} WHERE id = @Id";
            Console.WriteLine(sql);

            return await Connection.ExecuteAsync(sql, parameters) != 0;
        });
    }

    /// <summary>
    /// Replaces an article attachment file.
    /// </summary>
    /// <param name="id">The article id.</param>
    /// <param name="fileName">The attachment file name.</param>
    /// <param name="base64">The replacement attachment content encoded as Base64.</param>
    /// <returns>True when the article exists and the file is saved; otherwise, false.</returns>
    public async Task<bool> UpdateArticleAttachmentAsync(int id, string fileName, string base64)
    {
        if (!await ExistsAsync(id))
            return false;
        
        await FileSystemHelper.SaveArticleFileAsync(id, fileName, base64);
        return true;
    }

    /// <summary>
    /// Deletes an article by id.
    /// </summary>
    /// <param name="id">The article id.</param>
    /// <returns>True when the article exists and is deleted; otherwise, false.</returns>
    public async Task<bool> DeleteArticleAsync(int id)
    {
        if (!await ExistsAsync(id))
            return false;
        
        return await ExecuteSafelyAsync(async () => 
            await Connection.ExecuteAsync("DELETE FROM articles WHERE id = @Id", new { Id = id }) != 0);
    }

    /// <summary>
    /// Deletes an article attachment file and its database record.
    /// </summary>
    /// <param name="id">The article id.</param>
    /// <param name="fileName">The attachment file name.</param>
    /// <returns>True when the article exists and the database record is deleted; otherwise, false.</returns>
    public async Task<bool> DeleteArticleAttachmentAsync(int id, string fileName)
    {
        if (!await ExistsAsync(id))
            return false;

        FileSystemHelper.DeleteArticleAttachment(id, fileName);
        
        return await ExecuteSafelyAsync(async () => 
            await Connection.ExecuteAsync(
                "DELETE FROM article_attachments WHERE article_id = @Id AND file_name = @FileName",
                new
                {
                    ArticleId = id,
                    FileName = fileName
                }) !=
            0); 
    }

    /// <summary>
    /// Gets all article categories.
    /// </summary>
    /// <returns>An array of article categories, or null when the query fails.</returns>
    public async Task<ArticleCategory[]?> GetCategoriesAsync() =>
        await ExecuteSafelyAsync(async () => 
            (await Connection.QueryAsync<ArticleCategory>(
                "SELECT * FROM article_categories")).ToArray());
    
    /// <summary>
    /// Gets all article categories with articles assigned to each category.
    /// </summary>
    /// <returns>An array of article categories with articles, or null when a query fails.</returns>
    public async Task<ArticleCategory[]?> GetCategoriesWithArticlesAsync()
    {
        var categories = await GetCategoriesAsync();
        if (categories is null)
            return null;

        foreach (var category in categories)
        {
            var articles = await GetArticlesByCategoryAsync(category.Id);
            if (articles is null)
                return null;
            category.Articles = articles;
        }
        
        return categories;
    }

    /// <summary>
    /// Saves a new article category.
    /// </summary>
    /// <param name="req">The category data to save.</param>
    /// <returns>True when the category is inserted; otherwise, false.</returns>
    public async Task<bool> SaveArticleCategoryAsync(ArticleCategoryUploadRequest req) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteAsync(
                """
                INSERT INTO article_categories(label, name)
                VALUES (@Label, @Name);
                """, new { req.Label, req.Name })) != 0;

    private async Task<ArticleCategory?> GetCategoryModelAsync(string categoryName) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.QueryFirstOrDefaultAsync<ArticleCategory>(
                "SELECT * FROM article_categories WHERE name = @Name", new { Name = categoryName }));
    
    /// <summary>
    /// Assigns an article to an article category.
    /// </summary>
    /// <param name="categoryName">The category name.</param>
    /// <param name="request">The article assignment data.</param>
    /// <returns>True when the category exists and the assignment is inserted; otherwise, false.</returns>
    public async Task<bool> AssignArticleToCategoryAsync(string categoryName, AssignArticleToCategoryRequest request)
    {
        var category = await GetCategoryModelAsync(categoryName);
        if (category is null)
            return false;

        return await InsertArticleCategoryAssignmentAsync(request.ArticleId, category.Id, request.Order);
    }

    private async Task<bool> InsertArticleCategoryAssignmentAsync(int articleId, int categoryId, int order) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteAsync(
                """
                INSERT INTO article_category_assignment(article_id, category_id, "order")
                VALUES (@ArticleId, @CategoryId, @Order)
                """,
                new
                {
                    ArticleId = articleId,
                    CategoryId = categoryId,
                    Order = order
                })) != 0;

    /// <summary>
    /// Deletes an article category by category name.
    /// </summary>
    /// <param name="categoryName">The category name.</param>
    /// <returns>True when the category is deleted; otherwise, false.</returns>
    public async Task<bool> DeleteCategoryAsync(string categoryName) =>
        await Connection.ExecuteAsync(
            "DELETE FROM article_categories WHERE name = @CategoryName", new { CategoryName = categoryName }) != 0;

    /// <summary>
    /// Removes an article from an article category.
    /// </summary>
    /// <param name="categoryName">The category name.</param>
    /// <param name="articleId">The article id.</param>
    /// <returns>True when the category exists and the assignment is deleted; otherwise, false.</returns>
    public async Task<bool> DeleteArticleCategoryAssignmentAsync(string categoryName, int articleId)
    {
        var category = await GetCategoryModelAsync(categoryName);
        if (category is null)
            return false;

        return await DeleteArticleCategoryAssignmentAsync(category.Id, articleId);
    }
    
    private async Task<bool> DeleteArticleCategoryAssignmentAsync(int categoryId, int articleId) =>
        await ExecuteSafelyAsync(async () => 
            await Connection.ExecuteAsync(
                "DELETE FROM article_category_assignment WHERE article_id = @Id AND category_id = @CategoryId",
                new
                {
                    ArticleId = articleId,
                    CategoryId = categoryId
                }) !=
            0);
}
