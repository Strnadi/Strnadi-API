using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Administration.Api.Pages;

public class StatusCodeModel : PageModel
{
    public int Code { get; private set; }

    public void OnGet(int code)
    {
        Code = code;
    }
}
