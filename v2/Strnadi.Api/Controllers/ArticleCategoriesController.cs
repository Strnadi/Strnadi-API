using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Articles;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("articles/categories")]
public class ArticleCategoriesController(ArticleCategoriesService categoriesService) : ControllerBase
{
    /// <summary>All article categories, optionally with their articles.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllAsync([FromQuery] bool articles, CancellationToken cancellationToken)
    {
        return Ok(await categoriesService.GetAllAsync(articles, cancellationToken));
    }

    /// <summary>Creates a category. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] ArticleCategoryUploadRequest request, CancellationToken cancellationToken)
    {
        return Ok(await categoriesService.CreateAsync(request, cancellationToken));
    }

    /// <summary>A category's translation.</summary>
    [HttpGet("translations/{id:int}")]
    public async Task<IActionResult> GetTranslationAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
        return Ok(await categoriesService.GetTranslationAsync(id, cancellationToken));
    }

    /// <summary>Updates a category's translation.</summary>
    [Authorize]
    [HttpPatch("translations/{id:int}")]
    public async Task<IActionResult> UpdateTranslationAsync([FromRoute] int id, [FromBody] ArticleCategoryTranslationUpdateRequest request, CancellationToken cancellationToken)
    {
        await categoriesService.UpdateTranslationAsync(id, request, cancellationToken);
        return Ok();
    }

    /// <summary>Deletes a category's translation.</summary>
    [Authorize]
    [HttpDelete("translations/{id:int}")]
    public async Task<IActionResult> DeleteTranslationAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
        await categoriesService.DeleteTranslationAsync(id, cancellationToken);
        return Ok();
    }

    /// <summary>Deletes a category. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{categoryName}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] string categoryName, CancellationToken cancellationToken)
    {
        await categoriesService.DeleteAsync(categoryName, cancellationToken);
        return Ok();
    }

    /// <summary>Unassigns an article from a category. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpDelete("{categoryName}/{articleId:int}")]
    public async Task<IActionResult> RemoveAssignmentAsync([FromRoute] string categoryName, [FromRoute] int articleId, CancellationToken cancellationToken)
    {
        await categoriesService.RemoveAssignmentAsync(categoryName, articleId, cancellationToken);
        return Ok();
    }
}
