using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Articles;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("articles")]
public class ArticlesController : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAllAsync(CancellationToken cancellationToken)
    {
    }

    [HttpGet("{categoryName}")]
    public async Task<IActionResult> GetByCategoryAsync([FromRoute] string categoryName, CancellationToken cancellationToken)
    {
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetByIdAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
    }

    [HttpGet("{id:int}/{fileName}")]
    public async Task<IActionResult> GetAttachmentAsync([FromRoute] int id, [FromRoute] string fileName, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> CreateAsync([FromBody] ArticleUploadRequest request, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpPost("{id:int}/{fileName}")]
    public async Task<IActionResult> UploadAttachmentAsync([FromRoute] int id, [FromRoute] string fileName, [FromBody] string base64, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpPatch("{id:int}")]
    public async Task<IActionResult> UpdateAsync([FromRoute] int id, [FromBody] ArticleUpdateRequest request, CancellationToken cancellationToken)
    {
    }

    [HttpGet("translations/{id:int}")]
    public async Task<IActionResult> GetTranslationAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpPatch("translations/{id:int}")]
    public async Task<IActionResult> UpdateTranslationAsync([FromRoute] int id, [FromBody] ArticleTranslationUpdateRequest request, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpDelete("translations/{id:int}")]
    public async Task<IActionResult> DeleteTranslationAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("{id:int}/{fileName}")]
    public async Task<IActionResult> UpdateAttachmentAsync([FromRoute] int id, [FromRoute] string fileName, [FromBody] string base64, CancellationToken cancellationToken)
    {
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpPatch("{categoryName}")]
    public async Task<IActionResult> AssignToCategoryAsync([FromRoute] string categoryName, [FromBody] AssignArticleToCategoryRequest request, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteAsync([FromRoute] int id, CancellationToken cancellationToken)
    {
    }

    [Authorize]
    [HttpDelete("{id:int}/{fileName}")]
    public async Task<IActionResult> DeleteAttachmentAsync([FromRoute] int id, [FromRoute] string fileName, CancellationToken cancellationToken)
    {
    }
}
