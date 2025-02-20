namespace Shared.Communication;

public interface IHttpRequestResult
{
    HttpResponseMessage Message { get; }
}

public record HttpRequestResult : IHttpRequestResult
{
    public HttpResponseMessage Message { get; }
    
    public bool Success => Message.IsSuccessStatusCode;

    public HttpRequestResult(HttpResponseMessage message)
    {
        Message = message;
    }
}

public record HttpRequestResult<TResponse> : IHttpRequestResult
{
    public HttpResponseMessage Message { get; }
    
    public bool Success => Message.IsSuccessStatusCode;
    
    public TResponse? Value { get; }

    public HttpRequestResult(HttpResponseMessage message, TResponse? value)
    {
        Message = message;
        Value = value;
    }
}