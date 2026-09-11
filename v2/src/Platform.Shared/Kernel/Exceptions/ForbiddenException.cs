namespace Platform.Shared.Kernel.Exceptions;

public class ForbiddenException(string message)
    : Exception(message);
