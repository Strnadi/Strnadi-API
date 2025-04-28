using Auth.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Repository;
using Shared.Extensions;
using Shared.Models.Requests;
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
        var fileHelper = new FileSystemHelper();
        var article = await articlesRepo.GetAsync(id, fileName);

        return File(article, fileHelper.CreateArticleAttachmentPath(id, fileName));
    }

    [HttpPost]
    public async Task<IActionResult> Post([FromBody] ArticleUploadRequest req,
        [FromServices] JwtService jwtService,
        [FromServices] ArticlesRepository articlesRepo)
    {
        string? jwt = this.GetJwt();
        
        if (jwt is null)
            return BadRequest("No JWT provided");
        
        if (!jwtService.TryValidateToken(jwt, out string? email))
            return Unauthorized();

        int? id = await articlesRepo.SaveArticleAsync(req);

        return id is not null ? Ok(id) : StatusCode(500, "Failed to save article");
    }
}