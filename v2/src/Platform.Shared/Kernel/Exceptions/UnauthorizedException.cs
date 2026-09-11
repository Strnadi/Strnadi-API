namespace Platform.Shared.Kernel.Exceptions;

public class UnauthorizedException(string? message = "")
    : Exception(message);
