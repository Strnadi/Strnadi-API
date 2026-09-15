using System.ComponentModel.DataAnnotations;
using Administration.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Administration.Api.Pages.Account;

public class ForgotPasswordModel(UserManager<User> users, IEmailSender<User> emailSender, ILogger<ForgotPasswordModel> logger) : PageModel
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public bool LinkSent { get; set; }

    public class InputModel
    {
        [Required(ErrorMessage = "FieldRequired"), EmailAddress(ErrorMessage = "EmailInvalid")]
        public string Email { get; set; } = string.Empty;
    }

    public void OnGet()
    {
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var user = await users.FindByEmailAsync(Input.Email);
        if (user is not null)
        {
            var token = await users.GeneratePasswordResetTokenAsync(user);
            var resetLink = $"{Request.Scheme}://{Request.Host}/account/reset-password" +
                $"?email={Uri.EscapeDataString(user.Email!)}&token={Uri.EscapeDataString(token)}";

            await emailSender.SendPasswordResetLinkAsync(user, user.Email!, resetLink);
            logger.LogInformation("Sent password reset email to user {UserId}", user.Id);
        }

        // Don't reveal whether the email exists - always show the same confirmation.
        LinkSent = true;
        return Page();
    }
}
