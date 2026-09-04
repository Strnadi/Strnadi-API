using Microsoft.Extensions.Logging;
using Tenant.Domain.Entities;
using Tenant.Domain.Exceptions;
using Tenant.Domain.Persistence;
using Tenant.Domain.Persistence.Repositories;
using Tenant.Domain.Services;

namespace Tenant.Application.Photos;

public class PhotosService(IFileStorage fileStorage, IPhotosRepository photos, IUnitOfWork unitOfWork, ILogger<PhotosService> logger)
{
    public async Task<UserProfilePhotoModel> GetUserProfilePhotoAsync(int userId, CancellationToken cancellationToken = default)
    {
        var photo = await photos.GetByUserIdAsync(userId, cancellationToken) ?? throw new NotFoundException(nameof(Photo), userId);
        if (photo.FilePath is null || photo.Format is null)
            throw new NotFoundException(nameof(Photo), userId);

        var content = await fileStorage.ReadAsync(photo.FilePath, cancellationToken) ?? throw new NotFoundException(nameof(Photo), userId);

        return new UserProfilePhotoModel(photo.Format, Convert.ToBase64String(content));
    }

    public async Task UploadUserProfilePhotoAsync(int userId,
        UserProfilePhotoModel request,
        CancellationToken cancellationToken = default)
    {
        string path = await fileStorage.SaveAsync($"users/u_{userId}.{request.Format}",
            Convert.FromBase64String(request.PhotoBase64), cancellationToken);

        var photo = await photos.GetByUserIdAsync(userId, cancellationToken);
        if (photo is null)
        {
            photo = new Photo { UserId = userId, Format = request.Format, FilePath = path };
            photos.Add(photo);
        }
        else
        {
            photo.Format = request.Format;
            photo.FilePath = path;
        }
        
        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation("Profile photo uploaded for user {UserId}", userId);
    }
}