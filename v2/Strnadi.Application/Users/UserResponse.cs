namespace Strnadi.Application.Users;

public record UserResponse(int Id, string? Email, string? Nickname, string FirstName, string LastName);