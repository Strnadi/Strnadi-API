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
    
    private async Task<ArticleAttachmentModel[]?> GetArticleFilesAsync(int id) => 
        await ExecuteSafelyAsync(async () => 
            (await Connection.QueryAsync<ArticleAttachmentModel>("SELECT * FROM articles WHERE id = @Id", new { Id = id })).ToArray());

    public async Task<ArticleModel?> GetAsync(int id)
    {
        var article = await GetArticleAsync(id);
        if (article is null)
            return null;
        
        article.Files = (await GetArticleFilesAsync(id))!;
        if (article.Files == null!)
            return null;
        
        return article;
    }

    private async Task<ArticleModel?> GetArticleAsync(int id) =>
        await ExecuteSafelyAsync(async () =>
            await Connection.QueryFirstOrDefaultAsync<ArticleModel>(
                "SELECT * FROM articles WHERE id = @Id", new { Id = id }));

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
}