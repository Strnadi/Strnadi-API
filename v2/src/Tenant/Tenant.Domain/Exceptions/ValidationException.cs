namespace Tenant.Domain.Exceptions;

public class ValidationException(string message)
    : Exception(message);