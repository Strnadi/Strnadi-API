namespace Administration.Application.Auth;

public record ResetPasswordRequest(string Email, string Token, string NewPassword);