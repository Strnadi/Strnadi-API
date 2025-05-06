using Auth.Services;
using Microsoft.AspNetCore.Mvc;
using Repository;
using Shared.Extensions;
using Shared.Models.Requests.Articles;
using Shared.Tools;

namespace Articles;

[ApiController]
[Route("articles")]
public class ArticlesController : ControllerBase
{
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

    [HttpGet("{categoryName}")]
    public async Task<IActionResult> Get([FromRoute] string categoryName,
        [FromServices] ArticlesRepository articlesRepo)
    {
        var article = await articlesRepo.GetAsync(categoryName);
        if (article is null)
            return StatusCode(500, "Failed to get article");

        return Ok(article);
    }
    
    [HttpGet("{id:int}")]
    public async Task<IActionResult> Get([FromServices] ArticlesRepository articlesRepo, [FromRoute] int id)
    {
        var article = await articlesRepo.GetAsync(id);
        if (article is null)
            return StatusCode(500, "Failed to get article");

        return Ok(article);
    }

    [HttpGet("{id:int}/{fileName}")]
    public async Task<IActionResult> Get([FromServices] ArticlesRepository articlesRepo, 
        [FromRoute] int id,
        [FromRoute] string fileName)
    {
        var article = await articlesRepo.GetAsync(id, fileName);

        return File(article, MimeHelper.GetMimeType(FileSystemHelper.CreateArticleAttachmentPath(id, fileName)));
    }

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

    [HttpPost("/articles/{id:int}/{fileName}")]
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

    [HttpPatch("/articles/{id:int}")]
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

        bool success = await articlesRepo.UpdateArticle(id, req);
        
        return success ? Ok() : StatusCode(500, "Failed to save article");
    }

    [HttpPatch("/articles/{id:int}/{fileName}")]
    public async Task<IActionResult> Patch([FromRoute] int id,
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

        bool success = await articlesRepo.UpdateArticleAttachment(id, fileName, base64);
        
        return success ? Ok() : StatusCode(500, "Failed to save article");
    }

    [HttpDelete("/articles/{id:int}")]
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

    [HttpDelete("/articles/{id:int}/{fileName}")]
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
}