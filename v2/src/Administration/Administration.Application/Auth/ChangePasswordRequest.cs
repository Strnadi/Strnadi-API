namespace Administration.Application.Auth;

public record ChangePasswordRequest(string CurrentPassword, string NewPassword);