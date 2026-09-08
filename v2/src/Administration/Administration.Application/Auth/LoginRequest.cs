namespace Administration.Application.Auth;

public record LoginRequest(string Email, string Password, string? ReturnUrl);
