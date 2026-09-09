namespace Administration.Application.Users;

public record UserProfileResponse(
    Guid Id,
    string? UserName,
    string FirstName,
    string LastName,
    string? Email,
    string? City,
    int? PostCode,
    string[] Roles);

public record UpdateProfileRequest(
    string? UserName,
    string FirstName,
    string LastName,
    string? City,
    int? PostCode);
