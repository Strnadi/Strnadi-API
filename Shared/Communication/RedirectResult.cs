namespace Shared.Communication;

public interface IRedirectResult
{
    HttpResponseMessage Message { get; }
}

public record RedirectResult<TResponse>(TResponse? Value, HttpResponseMessage Message) : IRedirectResult;