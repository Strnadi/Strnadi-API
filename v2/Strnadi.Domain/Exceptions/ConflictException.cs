namespace Strnadi.Domain.Exceptions;

public class ConflictException(string message)
    : Exception(message);