using Microsoft.AspNetCore.Mvc;
using Shared.Communication;
using Shared.Logging;

namespace Shared.Extensions;

public static class ControllerExtensions
{
    public static async Task<IActionResult> HandleErrorResponseAsync(this ControllerBase controller, IHttpRequestResult? httpRequestResult) =>
        await HandleErrorResponseAsync(controller, httpRequestResult?.Message);
    
    public static async Task<IActionResult> HandleErrorResponseAsync(this ControllerBase controller, HttpResponseMessage? response)
    {
        if (response is null)
            return controller.StatusCode(500);

        Logger.Log($"Operation {controller.HttpContext.Request.Path} failed with status code " + (int)response.StatusCode);
        
        int statusCode = (int)response.StatusCode;
        string? content = response.Content != null!
            ? await response.Content.ReadAsStringAsync() 
            : null;
        
        return controller.StatusCode(statusCode, content);
    }

    public static string? GetJwt(this ControllerBase controller) => controller.HttpContext.GetJwt();
}