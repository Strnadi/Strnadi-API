using Strnadi.Domain.Entities;
using Strnadi.Domain.Exceptions;
using Strnadi.Domain.Persistence;
using Strnadi.Domain.Persistence.Repositories;
using Strnadi.Domain.Services;

namespace Strnadi.Application.Users;

public class UsersService(IUsersRepository users, 
    IUnitOfWork unitOfWork, 
    ITokenService tokenService,
    IPasswordHasher passwordHasher)
{
    public async Task<User[]> GetAllUsersAsync(CancellationToken cancellationToken = default)
    {
        return await users.GetAllAsync(cancellationToken);
    }

    public async Task<UserResponse> GetUserByIdAsync(int id, int? callerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(User), id);

        bool canSeeEmail = isAdmin || callerId == user.Id;
        
        return new UserResponse(user.Id, canSeeEmail ? user.Email : null, user.Nickname, user.FirstName, user.LastName);
    }
    
    public async Task<UserResponse> UpdateAsync(int id, UpdateUserRequest request, int callerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(User), id);

        if (!isAdmin && callerId != user.Id)
            throw new ForbiddenException("You can only edit your own profile");

        user.Nickname = request.Nickname ?? user.Nickname;
        user.FirstName = request.FirstName ?? user.FirstName;
        user.LastName = request.LastName ?? user.LastName;
        user.City = request.City ?? user.City;
        user.PostCode = request.PostCode ?? user.PostCode;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        bool canSeeEmail = isAdmin || callerId == user.Id;
        return new UserResponse(user.Id, canSeeEmail ? user.Email : null, user.Nickname, user.FirstName, user.LastName);
    }

    public async Task DeleteAsync(int id, int callerId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(id, cancellationToken) ?? throw new NotFoundException(nameof(User), id);

        if (!isAdmin && callerId != user.Id)
            throw new ForbiddenException("You can only delete your own profile");

        users.Remove(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> VerifyEmailAsync(int userId, string token, CancellationToken cancellationToken)
    {
        if (!tokenService.ValidateToken(token, out int userIdFromToken))
            return false;

        var user = await users.GetByIdAsync(userId, cancellationToken);
        if (user is null)
            return false;

        if (user.Id != userIdFromToken)
            return false;

        user.IsEmailVerified = true;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task ChangePasswordAsync(int userId, 
        ChangePasswordRequest request,
        int callerId,
        CancellationToken cancellationToken = default)
    {
        if (callerId != userId)
            throw new ForbiddenException("You can only change your own password");

        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException(nameof(User), userId);

        user.Password = passwordHasher.Hash(request.NewPassword);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(int? userId, string? email, CancellationToken cancellationToken = default)
    {
        if (email is not null)
            return await users.ExistsAsync(email, cancellationToken);

        return await users.ExistsAsync(userId!.Value, cancellationToken);
    }
}