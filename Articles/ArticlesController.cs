using Auth.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Repository;
using Shared.Extensions;
using Shared.Logging;
using Shared.Models.Database.Articles;
using Shared.Models.Requests.Articles;
using Shared.Tools;

namespace Articles;

/// <summary>
/// Provides endpoints for reading, creating, updating, categorizing, and deleting articles and article attachments.
/// </summary>
[ApiController]
[Route("articles")]
public class ArticlesController : ControllerBase
{
    /// <summary>
    /// Gets all articles with their attachments and categories.
    /// </summary>
    /// <param name="articlesRepo">Repository used to read article data.</param>
    /// <returns>An article collection, no content when no articles exist, or an error response.</returns>
    [HttpGet]
    [ProducesResponseType(typeof(Article[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
    /// Gets article categories, optionally including articles assigned to each category.
    /// </summary>
    /// <param name="articlesRepo">Repository used to read category data.</param>
    /// <param name="articles">Whether to include assigned articles in each category.</param>
    /// <returns>A category collection, no content when no categories exist, or an error response.</returns>
    [HttpGet("categories")]
    [ProducesResponseType(typeof(ArticleCategory[]), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
    /// Gets articles assigned to a category by category name.
    /// </summary>
    /// <param name="categoryName">The category name to query.</param>
    /// <param name="articlesRepo">Repository used to read article data.</param>
    /// <returns>The category article collection or an error response.</returns>
    [HttpGet("{categoryName}")]
    [ProducesResponseType(typeof(Article[]), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get([FromRoute] string categoryName,
        [FromServices] ArticlesRepository articlesRepo)
    {
        var article = await articlesRepo.GetAsync(categoryName);
        if (article is null)
            return StatusCode(500, "Failed to get article");

        return Ok(article);
    }
    
    /// <summary>
    /// Gets an article by id with its attachments and categories.
    /// </summary>
    /// <param name="articlesRepo">Repository used to read article data.</param>
    /// <param name="id">The article id.</param>
    /// <returns>The article or an error response.</returns>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(Article), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<IActionResult> Get([FromServices] ArticlesRepository articlesRepo, [FromRoute] int id)
    {
        var article = await articlesRepo.GetAsync(id);
        if (article is null)
            return StatusCode(500, "Failed to get article");

        return Ok(article);
    }

    /// <summary>
    /// Gets an article attachment file by article id and file name.
    /// </summary>
    /// <param name="articlesRepo">Repository used to read article attachment data.</param>
    /// <param name="id">The article id.</param>
    /// <param name="fileName">The attachment file name.</param>
    /// <returns>The attachment file or a not found response.</returns>
    [HttpGet("{id:int}/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
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
    /// Creates an article.
    /// </summary>
    /// <param name="req">The article data to save.</param>
    /// <param name="jwtService">Service used to validate the JWT from the request.</param>
    /// <param name="articlesRepo">Repository used to save article data.</param>
    /// <returns>The created article id or an error response.</returns>
    [HttpPost]
    [ProducesResponseType(typeof(int), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
    /// Saves an attachment file for an article.
    /// </summary>
    /// <param name="id">The article id.</param>
    /// <param name="fileName">The attachment file name.</param>
    /// <param name="base64">The attachment content encoded as Base64.</param>
    /// <param name="jwtService">Service used to validate the JWT from the request.</param>
    /// <param name="articlesRepo">Repository used to save article attachment data.</param>
    /// <returns>An ok response when the attachment is saved or an error response.</returns>
    [HttpPost("{id:int}/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
    /// Creates an article category.
    /// </summary>
    /// <param name="req">The category data to save.</param>
    /// <param name="articlesRepo">Repository used to save article category data.</param>
    /// <param name="usersRepo">Repository used to verify administrator access.</param>
    /// <param name="jwtService">Service used to validate the JWT from the request.</param>
    /// <returns>An ok response when the category is saved or an error response.</returns>
    [HttpPost("categories")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
    /// Updates an article by id.
    /// </summary>
    /// <param name="id">The article id.</param>
    /// <param name="req">The article fields to update.</param>
    /// <param name="jwtService">Service used to validate the JWT from the request.</param>
    /// <param name="articlesRepo">Repository used to update article data.</param>
    /// <returns>An ok response when the article is updated or an error response.</returns>
    [HttpPatch("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
    /// Updates an article attachment file.
    /// </summary>
    /// <param name="id">The article id.</param>
    /// <param name="fileName">The attachment file name.</param>
    /// <param name="base64">The replacement attachment content encoded as Base64.</param>
    /// <param name="jwtService">Service used to validate the JWT from the request.</param>
    /// <param name="usersRepo">Repository used to verify administrator access.</param>
    /// <param name="articlesRepo">Repository used to update article attachment data.</param>
    /// <returns>An ok response when the attachment is updated or an error response.</returns>
    [HttpPatch("{id:int}/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
    /// Assigns an article to a category.
    /// </summary>
    /// <param name="categoryName">The category name.</param>
    /// <param name="request">The article assignment data.</param>
    /// <param name="articlesRepo">Repository used to save the category assignment.</param>
    /// <param name="usersRepo">Repository used to verify administrator access.</param>
    /// <param name="jwtService">Service used to validate the JWT from the request.</param>
    /// <returns>An ok response when the assignment is saved or an error response.</returns>
    [HttpPatch("{categoryName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
    /// Deletes an article by id.
    /// </summary>
    /// <param name="id">The article id.</param>
    /// <param name="jwtService">Service used to validate the JWT from the request.</param>
    /// <param name="articlesRepo">Repository used to delete article data.</param>
    /// <returns>An ok response when the article is deleted or an error response.</returns>
    [HttpDelete("{id:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
    /// Deletes an article attachment file.
    /// </summary>
    /// <param name="id">The article id.</param>
    /// <param name="fileName">The attachment file name.</param>
    /// <param name="jwtService">Service used to validate the JWT from the request.</param>
    /// <param name="articlesRepo">Repository used to delete article attachment data.</param>
    /// <returns>An ok response when the attachment is deleted or an error response.</returns>
    [HttpDelete("{id:int}/{fileName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
    /// Deletes an article category by category name.
    /// </summary>
    /// <param name="categoryName">The category name.</param>
    /// <param name="jwtService">Service used to validate the JWT from the request.</param>
    /// <param name="usersRepo">Repository used to verify administrator access.</param>
    /// <param name="articlesRepo">Repository used to delete article category data.</param>
    /// <returns>An ok response when the category is deleted or an error response.</returns>
    [HttpDelete("categories/{categoryName}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
    /// Removes an article from a category.
    /// </summary>
    /// <param name="categoryName">The category name.</param>
    /// <param name="articleId">The article id.</param>
    /// <param name="jwtService">Service used to validate the JWT from the request.</param>
    /// <param name="usersRepo">Repository used to verify administrator access.</param>
    /// <param name="articlesRepo">Repository used to delete the category assignment.</param>
    /// <returns>An ok response when the assignment is deleted or an error response.</returns>
    [HttpDelete("categories/{categoryName}/{articleId:int}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
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
