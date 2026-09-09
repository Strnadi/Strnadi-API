using Administration.Application.Users;
using Administration.Domain.Entities;
using Administration.Domain.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Administration.Api.Controllers;

[ApiController]
[Route("users")]
public class UserPhotosController(UserManager<User> users, IFileStorage fileStorage) : ControllerBase
{
    [HttpGet("{userId:guid}/profile-photo")]
    public async Task<IActionResult> GetProfilePhotoAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user?.ProfilePhotoPath is null || user.ProfilePhotoFormat is null)
            return NotFound();

        var content = await fileStorage.ReadAsync(user.ProfilePhotoPath, cancellationToken);
        if (content is null)
            return NotFound();

        return Ok(new UserProfilePhotoModel(user.ProfilePhotoFormat, Convert.ToBase64String(content)));
    }
}
