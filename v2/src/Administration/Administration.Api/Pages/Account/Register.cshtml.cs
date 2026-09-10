using System.ComponentModel.DataAnnotations;
using Administration.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Administration.Api.Pages.Account;

public class RegisterModel(UserManager<User> users, SignInManager<User> signIn, IEmailSender<User> emailSender) : PageModel
{
    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "FieldRequired"), EmailAddress(ErrorMessage = "EmailInvalid")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired"), DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired"), DataType(DataType.Password), Compare(nameof(Password), ErrorMessage = "PasswordsDoNotMatch")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = new User
        {
            UserName = null,
            Email = Input.Email,
            FirstName = Input.FirstName,
            LastName = Input.LastName,
            CreatedAt = DateTime.UtcNow
        };

        var result = await users.CreateAsync(user, Input.Password);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }

        var token = await users.GenerateEmailConfirmationTokenAsync(user);
        var confirmLink = $"{Request.Scheme}://{Request.Host}/account/confirm-email" +
            $"?userId={user.Id}&token={Uri.EscapeDataString(token)}";

        await emailSender.SendConfirmationLinkAsync(user, user.Email!, confirmLink);

        await signIn.SignInAsync(user, isPersistent: true);
        return LocalRedirect(ReturnUrl ?? "/dashboard");
    }
}