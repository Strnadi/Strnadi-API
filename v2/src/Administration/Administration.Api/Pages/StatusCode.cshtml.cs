using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Administration.Api.Pages;

public class StatusCodeModel : PageModel
{
    public int Code { get; private set; }
    public string HeadingKey { get; private set; } = "SomethingWentWrong";
    public string? MessageKey { get; private set; }

    public void OnGet(int code)
    {
        Code = code;
        Response.StatusCode = code;

        (HeadingKey, MessageKey) = code switch
        {
            404 => ("PageNotFound", null),
            403 => ("AccessDenied", "AccessDeniedMessage"),
            _ => ("SomethingWentWrong", null)
        };
    }
}
