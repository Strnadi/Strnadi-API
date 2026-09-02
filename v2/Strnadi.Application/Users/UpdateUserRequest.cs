namespace Strnadi.Application.Users;

public record UpdateUserRequest(string? Nickname, string? FirstName, string? LastName, string? City, int? PostCode);
