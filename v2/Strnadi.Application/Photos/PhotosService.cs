using Strnadi.Domain.Entities;
using Strnadi.Domain.Exceptions;
using Strnadi.Domain.Persistence;
using Strnadi.Domain.Persistence.Repositories;
using Strnadi.Domain.Services;

namespace Strnadi.Application.Photos;

public class PhotosService(IFileStorage fileStorage, IPhotosRepository photos, IUnitOfWork unitOfWork)
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
    }
}