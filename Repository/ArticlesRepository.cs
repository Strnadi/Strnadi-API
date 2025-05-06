using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;
using Dapper;
using Microsoft.Extensions.Configuration;
using Shared.Models.Database.Articles;
using Shared.Models.Requests;
using Shared.Models.Requests.Articles;
using Shared.Tools;

namespace Repository;

public class ArticlesRepository : RepositoryBase
{
    public ArticlesRepository(IConfiguration configuration) : base(configuration)
    {
    }

    public async Task<ArticleModel[]?> GetAsync()
    {
        var articles = await GetArticlesAsync();
        
        if (articles is null)
            return null;

        foreach (var article in articles)
        {
            article.Files = (await GetArticleFilesAsync(article.Id))!;
            if (article.Files == null!)
                return null;
        }
        
        return articles;
    }

    private async Task<ArticleModel[]?> GetArticlesAsync() =>
        await ExecuteSafelyAsync(async () =>
            (await Connection.QueryAsync<ArticleModel>("SELECT * FROM articles")).ToArray());
    
    private async Task<ArticleAttachmentModel[]?> GetArticleFilesAsync(int articleId) => 
        await ExecuteSafelyAsync(async () => 
            (await Connection.QueryAsync<ArticleAttachmentModel>("SELECT * FROM article_attachments WHERE article_id = @ArticleId",
                new
                {
                    ArticleId = articleId
                })).ToArray());

    public async Task<ArticleModel[]?> GetAsync(string categoryName)
    {
        var assignments = await GetCategoryAssignmentsByCategory(categoryName);
        if (assignments is null)
            return null;

        var articles = new ArticleModel?[assignments.Length];
        for (int i = 0; i < articles.Length; i++)
        {
            articles[i] = await GetAsync(assignments[i].ArticleId);
            if (articles[i] is null)
                return null;
        }   
        return articles!;
    }

    private async Task<ArticleCategoryAssignment[]?> GetCategoryAssignmentsByCategory(string categoryName) =>
        await ExecuteSafelyAsync(async () =>
            (await Connection.QueryAsync<ArticleCategoryAssignment>(
                """
                SELECT * 
                FROM article_category_assignment 
                WHERE category_id = (
                    SELECT id 
                    FROM article_category_assignment 
                    WHERE name = @CategoryName
                )
                """, new { CategoryName = categoryName })).ToArray());

    public async Task<ArticleModel?> GetAsync(int id)
    {
        var article = await GetArticleAsync(id);
        if (article is null)
            return null;
        
        article.Files = (await GetArticleFilesAsync(id))!;
        if (article.Files == null!)
            return null;

        article.Categories = (await GetCategoriesByArticle(id))!;
        if (article.Categories == null!)
            return null;
        
        return article;
    }

    private async Task<ArticleModel?> GetArticleAsync(int id) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.QueryFirstOrDefaultAsync<ArticleModel>(
                "SELECT * FROM articles WHERE id = @Id", new { Id = id }));

    private async Task<ArticleCategoryModel[]?> GetCategoriesByArticle(int articleId)
    {
        var assignments = await GetCategoryAssignmentsByArticle(articleId);
        if (assignments is null)
            return null;
        
        var categories = new ArticleCategoryModel[assignments.Length];
        for (int i = 0; i < categories.Length; i++)
        {
            categories[i] = (await GetArticleCategory(assignments[i].CategoryId))!;
            if (categories[i] == null!)
                return null;
        }

        return categories;
    }

    private async Task<ArticleCategoryAssignment[]?> GetCategoryAssignmentsByArticle(int articleId) =>
        await ExecuteSafelyAsync(async () => 
            (await Connection.QueryAsync<ArticleCategoryAssignment>(
                "SELECT * FROM article_category_assignment WHERE article_id = @ArticleId ORDER BY \"order\"",
                new
                {
                    ArticleId = articleId
                })).ToArray());

    private async Task<ArticleCategoryModel?> GetArticleCategory(int categoryId) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.QueryFirstOrDefaultAsync<ArticleCategoryModel>(
                "SELECT * FROM article_categories WHERE id = @CategoryId", 
                new { CategoryId = categoryId }));

    public async Task<byte[]> GetAsync(int id, string fileName)
    {
        byte[] content = await FileSystemHelper.ReadArticleFileAsync(id, fileName);

        return content;
    }

    public async Task<int?> SaveArticleAsync(ArticleUploadRequest req) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.ExecuteScalarAsync<int?>(
                """
                INSERT INTO articles(name, description)
                VALUES (@Name, @Description)
                RETURNING id
                """, new { req.Name, req.Description }));

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
            await Connection.ExecuteScalarAsync<int>("SELECT * FROM articles WHERE id = @Id", new { Id = id }) != 0);

    public async Task<bool> UpdateArticle(int id, ArticleUpdateRequest req)
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

    public async Task<bool> UpdateArticleAttachment(int id, string fileName, string base64)
    {
        if (!await ExistsAsync(id))
            return false;
        
        await FileSystemHelper.SaveArticleFileAsync(id, fileName, base64);
        return true;
    }

    public async Task<bool> DeleteArticleAsync(int id)
    {
        if (!await ExistsAsync(id))
            return false;
        
        return await ExecuteSafelyAsync(async () => 
            await Connection.ExecuteAsync("DELETE FROM articles WHERE id = @Id", new { Id = id }) != 0);
    }

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
}