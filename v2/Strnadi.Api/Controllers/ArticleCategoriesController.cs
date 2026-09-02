using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Articles;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("articles/categories")]
public class ArticleCategoriesController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] bool articles, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] ArticleCategoryUploadRequest request, CancellationToken cancellationToken)
    {
    }

    [HttpGet("translations/{id:int}")]
    public async Task<IActionResult> GetTranslationAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpPatch("translations/{id:int}")]
    public async Task<IActionResult> UpdateTranslationAsync([FromRoute] int id, [FromBody] ArticleCategoryTranslationUpdateRequest request, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpDelete("translations/{id:int}")]
    public async Task<IActionResult> DeleteTranslationAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{categoryName}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] string categoryName, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{categoryName}/{articleId:int}")]
    public async Task<IActionResult> RemoveAssignmentAsync([FromRoute] string categoryName, [FromRoute] int articleId, CancellationToken cancellationToken)
    {
    }
}
