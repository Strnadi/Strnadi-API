using Strnadi.Application.Common;
using Strnadi.Domain.Entities;
using Strnadi.Domain.Exceptions;
using Strnadi.Domain.Persistence;
using Strnadi.Domain.Persistence.Repositories;
using Strnadi.Domain.Services;

namespace Strnadi.Application.Auth;

public class AuthService(
    IUsersRepository users,
    IUnitOfWork unitOfWork,
    ITokenService tokenService,
    IPasswordHasher passwordHasher,
    IGoogleIdTokenValidator googleIdTokenValidator,
    IAppleIdTokenValidator appleIdTokenValidator,
    IEmailSender emailSender,
    LinkBuilder linkBuilder)
{
    public async Task<bool> IsEmailVerifiedAsync(int callerId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(callerId, cancellationToken) ?? throw new NotFoundException(nameof(User), callerId);
        return user.IsEmailVerified == true;
    }

    public async Task<AuthResponse> RenewTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        if (!tokenService.ValidateToken(token, out int userId, validateLifetime: false))
            throw new UnauthorizedException("Invalid JWT");

        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw new ConflictException("User does not exist");

        return new AuthResponse(tokenService.GenerateToken(user.Id, user.Email!, user.Role));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(email, cancellationToken) ?? throw new ConflictException("User doesn't exist");

        if (user.Password is null || !passwordHasher.Verify(request.Password, user.Password))
            throw new UnauthorizedException("Invalid password");

        return new AuthResponse(tokenService.GenerateToken(user.Id, user.Email!, user.Role));
    }

    public async Task<AuthResponse> SignUpAsync(SignUpRequest request, CancellationToken cancellationToken = default)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await users.ExistsAsync(email, cancellationToken))
            throw new ConflictException("User already exists");

        bool regularRegister = request.Password is not null;

        var user = new User
        {
            Email = email,
            Nickname = request.Nickname,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PostCode = request.PostCode,
            City = request.City,
            Consent = request.Consent,
            Password = regularRegister ? passwordHasher.Hash(request.Password!) : null,
            GoogleId = request.GoogleId,
            Appleid = request.AppleId,
            Role = "user",
            CreationDate = DateTime.UtcNow,
            IsEmailVerified = !regularRegister,
            Legacy = false,
            Deleted = false
        };

        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var jwt = tokenService.GenerateToken(user.Id, user.Email!, user.Role);

        if (regularRegister)
        {
            var link = linkBuilder.VerificationLink(user.Id, jwt);
            await emailSender.SendEmailVerificationAsync(user.Email!, user.Nickname, link, cancellationToken);
        }

        return new AuthResponse(jwt);
    }

    public async Task ResendVerificationEmailAsync(int userId, int callerId, CancellationToken cancellationToken = default)
    {
        if (callerId != userId)
            throw new ForbiddenException("You can only resend verification for your own account");

        var user = await users.GetByIdAsync(userId, cancellationToken) ?? throw new NotFoundException(nameof(User), userId);

        if (user.IsEmailVerified == true)
            throw new ConflictException("Email is already verified");

        var jwt = tokenService.GenerateToken(user.Id, user.Email!, user.Role);
        var link = linkBuilder.VerificationLink(user.Id, jwt);
        await emailSender.SendEmailVerificationAsync(user.Email!, user.Nickname, link, cancellationToken);
    }

    public async Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(email, cancellationToken) ?? throw new NotFoundException(nameof(User), email);

        var jwt = tokenService.GenerateToken(user.Id, user.Email!, user.Role);
        var link = linkBuilder.PasswordResetLink(user.Id, jwt);
        await emailSender.SendPasswordResetAsync(user.Email!, user.Nickname, link, cancellationToken);
    }

    public async Task<bool> HasGoogleIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user?.GoogleId is not null;
    }

    public async Task<bool> HasAppleIdAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdAsync(userId, cancellationToken);
        return user?.Appleid is not null;
    }

    public async Task<GoogleSignUpResponse> SignUpGoogleAsync(GoogleAuthRequest request, CancellationToken cancellationToken = default)
    {
        var payload = await googleIdTokenValidator.ValidateAsync(request.IdToken, cancellationToken)
            ?? throw new UnauthorizedException("Invalid ID token");

        if (await users.ExistsAsync(payload.Email, cancellationToken))
            throw new ConflictException("User already exists");

        return new GoogleSignUpResponse(payload.Email, payload.Subject, payload.GivenName, payload.FamilyName);
    }

    public async Task<AuthResponse> LoginGoogleAsync(GoogleAuthRequest request, CancellationToken cancellationToken = default)
    {
        var payload = await googleIdTokenValidator.ValidateAsync(request.IdToken, cancellationToken)
            ?? throw new UnauthorizedException("Invalid ID token");

        var user = await users.GetByEmailAsync(payload.Email, cancellationToken) ?? throw new ConflictException("User doesn't exist");

        await MarkVerifiedAsync(user, cancellationToken);

        return new AuthResponse(tokenService.GenerateToken(user.Id, user.Email!, user.Role));
    }

    public async Task<SocialAuthResponse?> GoogleAsync(GoogleAuthRequest request, int? callerId, CancellationToken cancellationToken = default)
    {
        var payload = await googleIdTokenValidator.ValidateAsync(request.IdToken, cancellationToken)
            ?? throw new UnauthorizedException("Invalid ID token");

        if (callerId is not null)
        {
            var caller = await users.GetByIdAsync(callerId.Value, cancellationToken) ?? throw new NotFoundException(nameof(User), callerId.Value);
            caller.GoogleId = payload.Subject;
            users.Update(caller);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }

        var linkedUser = await users.GetByGoogleIdAsync(payload.Subject, cancellationToken);
        if (linkedUser is not null)
        {
            await MarkVerifiedAsync(linkedUser, cancellationToken);
            var jwt = tokenService.GenerateToken(linkedUser.Id, linkedUser.Email!, linkedUser.Role);
            return new SocialAuthResponse(true, jwt, linkedUser.Email, linkedUser.FirstName, linkedUser.LastName);
        }

        var existingByEmail = await users.GetByEmailAsync(payload.Email, cancellationToken);
        if (existingByEmail is not null)
        {
            existingByEmail.GoogleId = payload.Subject;
            users.Update(existingByEmail);
            await MarkVerifiedAsync(existingByEmail, cancellationToken);
            var jwt = tokenService.GenerateToken(existingByEmail.Id, existingByEmail.Email!, existingByEmail.Role);
            return new SocialAuthResponse(true, jwt, existingByEmail.Email, existingByEmail.FirstName, existingByEmail.LastName);
        }

        return new SocialAuthResponse(false, null, payload.Email, payload.GivenName, payload.FamilyName);
    }

    public async Task<SocialAuthResponse?> AppleAsync(AppleAuthRequest request, int? callerId, CancellationToken cancellationToken = default)
    {
        var payload = await appleIdTokenValidator.ValidateAsync(request.IdToken, cancellationToken)
            ?? throw new UnauthorizedException("Invalid ID token");

        if (callerId is not null)
        {
            var caller = await users.GetByIdAsync(callerId.Value, cancellationToken) ?? throw new NotFoundException(nameof(User), callerId.Value);
            caller.Appleid = request.UserIdentifier;
            users.Update(caller);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return null;
        }

        var linkedUser = await users.GetByAppleIdAsync(request.UserIdentifier, cancellationToken);
        if (linkedUser is not null)
        {
            await MarkVerifiedAsync(linkedUser, cancellationToken);
            var jwt = tokenService.GenerateToken(linkedUser.Id, linkedUser.Email!, linkedUser.Role);
            return new SocialAuthResponse(true, jwt, linkedUser.Email, linkedUser.FirstName, linkedUser.LastName);
        }

        if (payload.Email is not null)
        {
            var existingByEmail = await users.GetByEmailAsync(payload.Email, cancellationToken);
            if (existingByEmail is not null)
            {
                existingByEmail.Appleid = request.UserIdentifier;
                users.Update(existingByEmail);
                await MarkVerifiedAsync(existingByEmail, cancellationToken);
                var jwt = tokenService.GenerateToken(existingByEmail.Id, existingByEmail.Email!, existingByEmail.Role);
                return new SocialAuthResponse(true, jwt, existingByEmail.Email, existingByEmail.FirstName, existingByEmail.LastName);
            }

            return new SocialAuthResponse(false, null, payload.Email, request.GivenName, request.FamilyName);
        }

        // Apple only discloses the email on the very first authorization; a returning user
        // we don't recognize by AppleId and who carries no email in the token can't be signed up.
        throw new ConflictException("Email is required for first-time Apple sign-in");
    }

    private async Task MarkVerifiedAsync(User user, CancellationToken cancellationToken)
    {
        if (user.IsEmailVerified == true)
            return;

        user.IsEmailVerified = true;
        users.Update(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
