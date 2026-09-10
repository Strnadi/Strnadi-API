using System.ComponentModel.DataAnnotations;
using Administration.Api.Resources;
using Administration.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;

namespace Administration.Api.Pages.Account;

public class ResetPasswordModel(UserManager<User> users, IStringLocalizer<SharedResource> localizer) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "FieldRequired"), DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired"), DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "PasswordsDoNotMatch")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        if (Email is null || Token is null)
        {
            ModelState.AddModelError(string.Empty, localizer["InvalidOrExpiredResetLink"]);
            return Page();
        }

        var user = await users.FindByEmailAsync(Email);
        if (user is null)
        {
            // Don't reveal whether the email exists.
            return RedirectToPage("Login");
        }

        var result = await users.ResetPasswordAsync(user, Token, Input.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }

        return RedirectToPage("Login");
    }
}
