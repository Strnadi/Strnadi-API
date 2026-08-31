using Auth.Services;
using Microsoft.AspNetCore.Mvc;
using Repository;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Requests.Articles;
using Shared.Tools;

namespace Articles;

[ApiController]
[Route("articles")]
public class ArticlesController : ControllerBase
{
    /// <summary>
    /// Lists all published articles.
    /// </summary>
    /// <returns>The array of articles, 204 if none exist, or 500 on failure.</returns>
    [HttpGet]
    public async Task<IActionResult> Get([FromServices] ArticlesRepository articlesRepo)
    {
        var articles = await articlesRepo.GetAsync();
        if (articles is null)
            return StatusCode(500, "Failed to get articles");

        if (articles.Length == 0)
            return NoContent();

        return Ok(articles);
    }

    /// <summary>
    /// Lists article categories.
    /// </summary>
    /// <param name="articles">Whether to include the articles assigned to each category. Defaults to <c>true</c>.</param>
    /// <returns>The array of categories, 204 if none exist, or 500 on failure.</returns>
    [HttpGet("categories")]
    public async Task<IActionResult> GetCategories([FromServices] ArticlesRepository articlesRepo,
        [FromQuery] bool articles = true)
    {
        var categories = await (articles
            ? articlesRepo.GetCategoriesWithArticlesAsync()
            : articlesRepo.GetCategoriesAsync());

        if (categories is null)
            return StatusCode(500, "Failed to get categories");

        if (categories.Length == 0)
            return NoContent();

        return Ok(categories);
    }

    /// <summary>
    /// Gets all articles assigned to a category.
    /// </summary>
    /// <param name="categoryName">Name of the category to look up articles for.</param>
    /// <returns>The matching articles, or 500 on failure.</returns>
    [HttpGet("{categoryName}")]
    public async Task<IActionResult> Get([FromRoute] string categoryName,
        [FromServices] ArticlesRepository articlesRepo)
    {
        var article = await articlesRepo.GetAsync(categoryName);
        if (article is null)
            return StatusCode(500, "Failed to get article");

        return Ok(article);
    }

    /// <summary>
    /// Gets a single article by its identifier.
    /// </summary>
    /// <param name="id">Identifier of the article to retrieve.</param>
    /// <returns>The article, or 500 on failure.</returns>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get([FromServices] ArticlesRepository articlesRepo, [FromRoute] int id)
    {
        var article = await articlesRepo.GetAsync(id);
        if (article is null)
            return StatusCode(500, "Failed to get article");

        return Ok(article);
    }

    /// <summary>
    /// Downloads an attachment file belonging to an article.
    /// </summary>
    /// <param name="id">Identifier of the article the attachment belongs to.</param>
    /// <param name="fileName">Name of the attachment file to download.</param>
    /// <returns>The attachment file contents, or 404 if it does not exist.</returns>
    [HttpGet("{id:int}/{fileName}")]
    public async Task<IActionResult> Get([FromServices] ArticlesRepository articlesRepo,
        [FromRoute] int id,
        [FromRoute] string fileName)
    {
        try
        {
            var article = await articlesRepo.GetAsync(id, fileName);
            if (article is null)
                return NotFound();

            return File(article, MimeHelper.GetMimeType(FileSystemHelper.CreateArticleAttachmentPath(id, fileName)));
        }
        catch (Exception ex)
        {
            Logger.Log(ex.Message, LogLevel.Error);
            return NotFound();
        }
    }

    /// <summary>
    /// Creates a new article. Requires a valid JWT.
    /// </summary>
    /// <param name="req">The article to create.</param>
    /// <returns>The identifier of the created article, 400 if the JWT is missing, 401 if it is invalid, or 500 on failure.</returns>
    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ArticleUploadRequest req,
        [FromServices] JwtService jwtService,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();

        int? id = await articlesRepo.SaveArticleAsync(req);

        return id is not null ? Ok(id) : StatusCode(500, "Failed to save article");
    }

    /// <summary>
    /// Uploads an attachment for an existing article. Requires a valid JWT.
    /// </summary>
    /// <param name="id">Identifier of the article to attach the file to.</param>
    /// <param name="fileName">Name to store the attachment file as.</param>
    /// <param name="base64">Base64-encoded contents of the attachment file.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid, or 500 on failure.</returns>
    [HttpPost("{id:int}/{fileName}")]
    public async Task<IActionResult> Post([FromRoute] int id,
        [FromRoute] string fileName,
        [FromBody] string base64,
        [FromServices] JwtService jwtService,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();

        bool success = await articlesRepo.SaveArticleAttachmentAsync(id, fileName, base64);

        return success ? Ok() : StatusCode(500, "Failed to save article");
    }

    /// <summary>
    /// Creates a new article category. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="req">The category to create.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid or the caller is not an administrator, or 500 on failure.</returns>
    [HttpPost("categories")]
    public async Task<IActionResult> PostCategories([FromBody] ArticleCategoryUploadRequest req,
        [FromServices] ArticlesRepository articlesRepo,
        [FromServices] UsersRepository usersRepo,
        [FromServices] JwtService jwtService)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("Only administrators can perform this action");

        bool success = await articlesRepo.SaveArticleCategoryAsync(req);

        return success ? Ok() : StatusCode(500, "Failed to save article category");
    }

    /// <summary>
    /// Updates an existing article. Requires a valid JWT.
    /// </summary>
    /// <param name="id">Identifier of the article to update.</param>
    /// <param name="req">The fields to update.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid, or 500 on failure.</returns>
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> Patch([FromRoute] int id,
        [FromBody] ArticleUpdateRequest req,
        [FromServices] JwtService jwtService,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();

        bool success = await articlesRepo.UpdateArticleAsync(id, req);

        return success ? Ok() : StatusCode(500, "Failed to save article");
    }

    /// <summary>
    /// Gets the translation of an article by its identifier.
    /// </summary>
    /// <param name="id">Identifier of the article translation to retrieve.</param>
    /// <returns>The article translation, or 404 if it does not exist.</returns>
    [HttpGet("translations/{id:int}")]
    public async Task<IActionResult> GetTranslation([FromRoute] int id,
        [FromServices] ArticlesRepository articlesRepo)
    {
        var translation = await articlesRepo.GetArticleTranslationAsync(id);
        if (translation is null)
            return NotFound();

        return Ok(translation);
    }

    /// <summary>
    /// Updates an article translation. Requires a valid JWT.
    /// </summary>
    /// <param name="id">Identifier of the article translation to update.</param>
    /// <param name="req">The fields to update.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid, or 500 on failure.</returns>
    [HttpPatch("translations/{id:int}")]
    public async Task<IActionResult> PatchTranslation([FromRoute] int id,
        [FromBody] ArticleTranslationUpdateRequest req,
        [FromServices] JwtService jwtService,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();
        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();

        bool success = await articlesRepo.UpdateArticleTranslationAsync(id, req);

        return success ? Ok() : StatusCode(500, "Failed to update article translation");
    }

    /// <summary>
    /// Deletes an article translation. Requires a valid JWT.
    /// </summary>
    /// <param name="id">Identifier of the article translation to delete.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid, or 500 on failure.</returns>
    [HttpDelete("translations/{id:int}")]
    public async Task<IActionResult> DeleteTranslation([FromRoute] int id,
        [FromServices] JwtService jwtService,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();
        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();

        bool success = await articlesRepo.DeleteArticleTranslationAsync(id);

        return success ? Ok() : StatusCode(500, "Failed to delete article translation");
    }

    /// <summary>
    /// Gets the translation of an article category by its identifier.
    /// </summary>
    /// <param name="id">Identifier of the article category translation to retrieve.</param>
    /// <returns>The article category translation, or 404 if it does not exist.</returns>
    [HttpGet("categories/translations/{id:int}")]
    public async Task<IActionResult> GetCategoryTranslation([FromRoute] int id,
        [FromServices] ArticlesRepository articlesRepo)
    {
        var translation = await articlesRepo.GetArticleCategoryTranslationAsync(id);
        if (translation is null)
            return NotFound();

        return Ok(translation);
    }

    /// <summary>
    /// Updates an article category translation. Requires a valid JWT.
    /// </summary>
    /// <param name="id">Identifier of the article category translation to update.</param>
    /// <param name="req">The fields to update.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid, or 500 on failure.</returns>
    [HttpPatch("categories/translations/{id:int}")]
    public async Task<IActionResult> PatchCategoryTranslation([FromRoute] int id,
        [FromBody] ArticleCategoryTranslationUpdateRequest req,
        [FromServices] JwtService jwtService,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();
        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();

        bool success = await articlesRepo.UpdateArticleCategoryTranslationAsync(id, req);

        return success ? Ok() : StatusCode(500, "Failed to update article category translation");
    }

    /// <summary>
    /// Deletes an article category translation. Requires a valid JWT.
    /// </summary>
    /// <param name="id">Identifier of the article category translation to delete.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid, or 500 on failure.</returns>
    [HttpDelete("categories/translations/{id:int}")]
    public async Task<IActionResult> DeleteCategoryTranslation([FromRoute] int id,
        [FromServices] JwtService jwtService,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();
        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();

        bool success = await articlesRepo.DeleteArticleCategoryTranslationAsync(id);

        return success ? Ok() : StatusCode(500, "Failed to delete article category translation");
    }

    /// <summary>
    /// Replaces an article attachment file. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="id">Identifier of the article the attachment belongs to.</param>
    /// <param name="fileName">Name of the attachment file to replace.</param>
    /// <param name="base64">Base64-encoded contents to replace the attachment with.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid or the caller is not an administrator, or 500 on failure.</returns>
    [HttpPatch("{id:int}/{fileName}")]
    public async Task<IActionResult> Patch([FromRoute] int id,
        [FromRoute] string fileName,
        [FromBody] string base64,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("Only administrators can perform this action");

        bool success = await articlesRepo.UpdateArticleAttachmentAsync(id, fileName, base64);

        return success ? Ok() : StatusCode(500, "Failed to save article");
    }

    /// <summary>
    /// Assigns an existing article to a category. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="categoryName">Name of the category to assign the article to.</param>
    /// <param name="request">Identifies the article to assign.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid or the caller is not an administrator, or 500 on failure.</returns>
    [HttpPatch("{categoryName}")]
    public async Task<IActionResult> AssignArticleToCategory([FromRoute] string categoryName,
        [FromBody] AssignArticleToCategoryRequest request,
        [FromServices] ArticlesRepository articlesRepo,
        [FromServices] UsersRepository usersRepo,
        [FromServices] JwtService jwtService)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("Only administrators can perform this action");

        bool success = await articlesRepo.AssignArticleToCategoryAsync(categoryName, request);

        return success ? Ok() : StatusCode(500, "Failed to save article");
    }

    /// <summary>
    /// Deletes an article. Requires a valid JWT.
    /// </summary>
    /// <param name="id">Identifier of the article to delete.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid, or 500 on failure.</returns>
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete([FromRoute] int id,
        [FromServices] JwtService jwtService,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();

        bool success = await articlesRepo.DeleteArticleAsync(id);

        return success ? Ok() : StatusCode(500, "Failed to save article");
    }

    /// <summary>
    /// Deletes an article attachment file. Requires a valid JWT.
    /// </summary>
    /// <param name="id">Identifier of the article the attachment belongs to.</param>
    /// <param name="fileName">Name of the attachment file to delete.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid, or 500 on failure.</returns>
    [HttpDelete("{id:int}/{fileName}")]
    public async Task<IActionResult> Delete([FromRoute] int id, [FromRoute] string fileName,
        [FromServices] JwtService jwtService,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out _))
            return Unauthorized();

        bool success = await articlesRepo.DeleteArticleAttachmentAsync(id, fileName);

        return success ? Ok() : StatusCode(500, "Failed to save article");
    }

    /// <summary>
    /// Deletes an article category. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="categoryName">Name of the category to delete.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid or the caller is not an administrator, or 500 on failure.</returns>
    [HttpDelete("categories/{categoryName}")]
    public async Task<IActionResult> DeleteCategory([FromRoute] string categoryName,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("Only administrators can perform this action");

        bool success = await articlesRepo.DeleteCategoryAsync(categoryName);

        Logger.Log(success ?
            $"Category {categoryName} deleted successfully" :
            $"Failed to delete category {categoryName}");

        return success ? Ok() : StatusCode(500, "Failed to delete category");
    }

    /// <summary>
    /// Removes an article's assignment to a category. Requires a valid JWT belonging to an administrator.
    /// </summary>
    /// <param name="categoryName">Name of the category to remove the article from.</param>
    /// <param name="articleId">Identifier of the article to unassign.</param>
    /// <returns>200 on success, 400 if the JWT is missing, 401 if it is invalid or the caller is not an administrator, or 500 on failure.</returns>
    [HttpDelete("categories/{categoryName}/{articleId:int}")]
    public async Task<IActionResult> DeleteCategory([FromRoute] string categoryName,
        [FromRoute] int articleId,
        [FromServices] JwtService jwtService,
        [FromServices] UsersRepository usersRepo,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();

        if (jwt is null)
            return BadRequest("No JWT provided");

        if (!jwtService.TryValidateToken(jwt, out string email))
            return Unauthorized();

        if (!await usersRepo.IsAdminAsync(email))
            return Unauthorized("Only administrators can perform this action");

        bool success = await articlesRepo.DeleteArticleCategoryAssignmentAsync(categoryName, articleId);

        return success ? Ok() : StatusCode(500, "Failed to delete article from category");
    }
}
