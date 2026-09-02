using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strnadi.Application.Photos;

namespace Strnadi.Api.Controllers;

[ApiController]
[Route("users")]
public class UserPhotosController(PhotosService photos) : ControllerBase
{
    [Authorize]
    [HttpPost("{userId:int}/upload-profile-photo")]
    [RequestSizeLimit(130023424)]
    public async Task<IActionResult> UploadUserProfilePhoto([FromRoute] int userId,
        [FromBody] UserProfilePhotoModel req)
    {
        await photos.UploadUserProfilePhotoAsync(userId, req);
        return Ok();   
    }
}