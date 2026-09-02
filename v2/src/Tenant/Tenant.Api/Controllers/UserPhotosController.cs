using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Tenant.Application.Photos;

namespace Tenant.Api.Controllers;

[ApiController]
[Route("users")]
public class UserPhotosController(PhotosService photos) : ControllerBase
{
    /// <summary>Uploads or replaces a user's profile photo.</summary>
    [Authorize]
    [HttpPost("{userId:int}/upload-profile-photo")]
    [RequestSizeLimit(130023424)]
    public async Task<IActionResult> UploadUserProfilePhoto([FromRoute] int userId,
        [FromBody] UserProfilePhotoModel req)
    {
        await photos.UploadUserProfilePhotoAsync(userId, req);
        return Ok();
    }

    /// <summary>A user's profile photo.</summary>
    [HttpGet("{userId:int}/get-profile-photo")]
    public async Task<IActionResult> GetUserProfilePhoto([FromRoute] int userId, CancellationToken cancellationToken)
    {
        return Ok(await photos.GetUserProfilePhotoAsync(userId, cancellationToken));
    }
}