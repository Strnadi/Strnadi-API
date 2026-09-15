using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Administration.Api.Pages;

public class ErrorModel : PageModel
{
    public string HeadingKey { get; private set; } = "SomethingWentWrong";
    public string? MessageKey { get; private set; }

    public IActionResult OnGet(int code)
    {
        Response.StatusCode = code;

        (HeadingKey, MessageKey) = code switch
        {
            404 => ("PageNotFound", null),
            403 => ("AccessDenied", "AccessDeniedMessage"),
            _ => ("SomethingWentWrong", null)
        };

        return Page();
    }
}
