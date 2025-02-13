using ApiGateway.Controllers;
using Microsoft.AspNetCore.Mvc;

namespace ApiGateway.Services;

public class LinkGenerator
{
    public string GenerateLink(string jwt, HttpContext httpContext, ControllerContext controllerContext)
    {
        string scheme = httpContext.Request.Scheme;
        string host = httpContext.Request.Host.ToUriComponent();
        string route = controllerContext.ActionDescriptor.AttributeRouteInfo!.Template!;
        
        string link = $"{scheme}://{host}{route}?jwt={jwt}";

        return link;
    }
}