using Administration.Domain.Entities;
using Administration.Domain.Exceptions;
using Administration.Domain.Persistence;
using Administration.Domain.Persistence.Repositories;
using Administration.Domain.Services;

namespace Administration.Application.Auth;

// Account mutations and credential validation only — token issuance is TokenController's job
// (the single /connect/token OpenIddict endpoint), not this service's.
public class AuthService(
    IUsersRepository users,
    IUnitOfWork unitOfWork,
    IPasswordHasher passwordHasher,
    IGoogleIdTokenValidator googleIdTokenValidator,
    IAppleIdTokenValidator appleIdTokenValidator)
{
    public async Task<User> SignUpAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        email = email.Trim().ToLowerInvariant();
        if (await users.ExistsAsync(email, cancellationToken))
            throw new ConflictException("User already exists");

        var user = new User
        {
            Email = email,
            PasswordHash = passwordHasher.Hash(password),
            IsEmailConfirmed = false,
            CreatedAt = DateTime.UtcNow,
            Deleted = false
        };

        users.Add(user);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return user;
    }

    public async Task<GoogleIdTokenPayload> SignUpGoogleAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var payload = await googleIdTokenValidator.ValidateAsync(idToken, cancellationToken)
            ?? throw new UnauthorizedException("Invalid ID token");

        if (await users.ExistsAsync(payload.Email, cancellationToken))
            throw new ConflictException("User already exists");

        return payload;
    }

    public async Task<AppleIdTokenPayload> SignUpAppleAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var payload = await appleIdTokenValidator.ValidateAsync(idToken, cancellationToken)
            ?? throw new UnauthorizedException("Invalid ID token");

        if (payload.Email is null)
            throw new ConflictException("Email is required for first-time Apple sign-in");

        if (await users.ExistsAsync(payload.Email, cancellationToken))
            throw new ConflictException("User already exists");

        return payload;
    }

    public async Task LinkGoogleAsync(int callerId, string idToken, CancellationToken cancellationToken = default)
    {
        var payload = await googleIdTokenValidator.ValidateAsync(idToken, cancellationToken)
            ?? throw new UnauthorizedException("Invalid ID token");

        var caller = await users.GetByIdAsync(callerId, cancellationToken) ?? throw new NotFoundException(nameof(User), callerId);
        caller.GoogleId = payload.Subject;
        users.Update(caller);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task LinkAppleAsync(int callerId, string userIdentifier, CancellationToken cancellationToken = default)
    {
        var caller = await users.GetByIdAsync(callerId, cancellationToken) ?? throw new NotFoundException(nameof(User), callerId);
        caller.AppleId = userIdentifier;
        users.Update(caller);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<User> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        email = email.Trim().ToLowerInvariant();
        var user = await users.GetByEmailAsync(email, cancellationToken) ?? throw new UnauthorizedException("Invalid credentials");

        if (user.PasswordHash is null || !passwordHasher.Verify(password, user.PasswordHash))
            throw new UnauthorizedException("Invalid credentials");

        return user;
    }

    public async Task<User> ValidateGoogleAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var payload = await googleIdTokenValidator.ValidateAsync(idToken, cancellationToken)
            ?? throw new UnauthorizedException("Invalid ID token");

        var user = await users.GetByGoogleIdAsync(payload.Subject, cancellationToken)
            ?? throw new UnauthorizedException("No account linked to this Google identity");

        return user;
    }

    public async Task<User> ValidateAppleAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var payload = await appleIdTokenValidator.ValidateAsync(idToken, cancellationToken)
            ?? throw new UnauthorizedException("Invalid ID token");

        var user = await users.GetByAppleIdAsync(payload.Subject, cancellationToken)
            ?? throw new UnauthorizedException("No account linked to this Apple identity");

        return user;
    }

    // Re-resolves the user (with current roles/permissions) for a refresh_token exchange, so a
    // permission revoked since the last token was issued actually takes effect on refresh.
    public async Task<User> GetForRefreshAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await users.GetByIdWithRolesAsync(userId, cancellationToken);
        if (user is null || user.Deleted)
            throw new UnauthorizedException("Account no longer exists");

        return user;
    }
}