namespace Administration.Application.Users;

/// <summary>What a caller with only ViewUsersBasic may see about another user.</summary>
public record UserBasicResponse(
    Guid Id,
    string FirstName,
    string LastName,
    DateTime CreatedAt,
    bool Deleted,
    bool Legacy,
    bool EmailConfirmed);

/// <summary>What a caller with ViewUsersConfidential (or ManageUsers) may see about another user.</summary>
public record UserConfidentialResponse(
    Guid Id,
    string FirstName,
    string LastName,
    DateTime CreatedAt,
    bool Deleted,
    bool Legacy,
    bool EmailConfirmed,
    string? Email,
    string? City,
    int? PostCode);
