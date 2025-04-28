using Dapper;
using Microsoft.Extensions.Configuration;
using Shared.Models.Database.Articles;
using Shared.Models.Requests;
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
}