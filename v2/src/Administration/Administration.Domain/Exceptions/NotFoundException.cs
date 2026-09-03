namespace Administration.Domain.Exceptions;

public class NotFoundException(string entity, object key)
    : Exception($"{entity} not found in {key}");