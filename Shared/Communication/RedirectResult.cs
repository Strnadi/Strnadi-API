namespace Shared.Communication;

public record RedirectResult<TResponse>(TResponse? Value, HttpResponseMessage Message);