using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Application.Articles;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("articles")]
public class ArticlesController(ArticlesService articlesService) : ControllerBase
{
    /// <summary>All articles.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
        return Ok(await articlesService.GetAllAsync(cancellationToken));
    }

    /// <summary>Articles in a category.</summary>
    [HttpGet("{categoryName}")]
    public async Task<IActionResult> GetByCategoryAsync([FromRoute] string categoryName, CancellationToken cancellationToken)
    {
        return Ok(await articlesService.GetByCategoryAsync(categoryName, cancellationToken));
    }

    /// <summary>An article by id.</summary>
    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
        return Ok(await articlesService.GetByIdAsync(id, cancellationToken));
    }

    /// <summary>Downloads a file attached to an article.</summary>
    [HttpGet("{id:int}/{fileName}")]
    public async Task<IActionResult> GetAttachmentAsync([FromRoute] int id, [FromRoute] string fileName, CancellationToken cancellationToken)
    {
        var content = await articlesService.GetAttachmentAsync(id, fileName, cancellationToken);
        return File(content, "application/octet-stream", fileName);
    }

    /// <summary>Creates an article.</summary>
    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] ArticleUploadRequest request, CancellationToken cancellationToken)
    {
        return Ok(await articlesService.CreateAsync(request, cancellationToken));
    }

    /// <summary>Attaches a file to an article.</summary>
    [Authorize]
    [HttpPost("{id:int}/{fileName}")]
    public async Task<IActionResult> UploadAttachmentAsync([FromRoute] int id, [FromRoute] string fileName, [FromBody] string base64, CancellationToken cancellationToken)
    {
        await articlesService.UploadAttachmentAsync(id, fileName, base64, cancellationToken);
        return Ok();
    }

    /// <summary>Updates an article.</summary>
    [Authorize]
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] ArticleUpdateRequest request, CancellationToken cancellationToken)
    {
        await articlesService.UpdateAsync(id, request, cancellationToken);
        return Ok();
    }

    /// <summary>An article's translation.</summary>
    [HttpGet("translations/{id:int}")]
    public async Task<IActionResult> GetTranslationAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
        return Ok(await articlesService.GetTranslationAsync(id, cancellationToken));
    }

    /// <summary>Updates an article's translation.</summary>
    [Authorize]
    [HttpPatch("translations/{id:int}")]
    public async Task<IActionResult> UpdateTranslationAsync([FromRoute] int id, [FromBody] ArticleTranslationUpdateRequest request, CancellationToken cancellationToken)
    {
        await articlesService.UpdateTranslationAsync(id, request, cancellationToken);
        return Ok();
    }

    /// <summary>Deletes an article's translation.</summary>
    [Authorize]
    [HttpDelete("translations/{id:int}")]
    public async Task<IActionResult> DeleteTranslationAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
        await articlesService.DeleteTranslationAsync(id, cancellationToken);
        return Ok();
    }

    /// <summary>Replaces an article's attachment. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("{id:int}/{fileName}")]
    public async Task<IActionResult> UpdateAttachmentAsync([FromRoute] int id, [FromRoute] string fileName, [FromBody] string base64, CancellationToken cancellationToken)
    {
        await articlesService.UpdateAttachmentAsync(id, fileName, base64, cancellationToken);
        return Ok();
    }

    /// <summary>Assigns an article to a category. Admin only.</summary>
    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("{categoryName}")]
    public async Task<IActionResult> AssignToCategoryAsync([FromRoute] string categoryName, [FromBody] AssignArticleToCategoryRequest request, CancellationToken cancellationToken)
    {
        await articlesService.AssignToCategoryAsync(categoryName, request, cancellationToken);
        return Ok();
    }

    /// <summary>Deletes an article.</summary>
    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
        await articlesService.DeleteAsync(id, cancellationToken);
        return Ok();
    }

    /// <summary>Deletes a file attached to an article.</summary>
    [Authorize]
    [HttpDelete("{id:int}/{fileName}")]
    public async Task<IActionResult> DeleteAttachmentAsync([FromRoute] int id, [FromRoute] string fileName, CancellationToken cancellationToken)
    {
        await articlesService.DeleteAttachmentAsync(id, fileName, cancellationToken);
        return Ok();
    }
}
