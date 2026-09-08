namespace Administration.Application.Auth;

public record RegisterRequest(string Email, string Password, string FirstName, string LastName);