namespace Platform.Shared.Kernel.Exceptions;

public class ValidationException(string message)
    : Exception(message);
