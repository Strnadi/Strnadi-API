using System.ComponentModel.DataAnnotations;
using Administration.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Administration.Api.Pages.Account;

public class ResetPasswordModel(UserManager<User> users) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? Email { get; set; }

    [BindProperty(SupportsGet = true)]
    public string? Token { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required, DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Compare(nameof(NewPassword))]
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
            ModelState.AddModelError(string.Empty, "Invalid or expired reset link.");
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
