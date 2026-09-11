using System.ComponentModel.DataAnnotations;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Administration.Api.Pages.Dashboard;

public class IndexModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions, ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    [BindProperty]
    public PasswordInputModel PasswordInput { get; set; } = new();

    public class InputModel
    {
        public string? UserName { get; set; }

        [Required(ErrorMessage = "FieldRequired")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired")]
        public string LastName { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired"), EmailAddress(ErrorMessage = "EmailInvalid")]
        public string Email { get; set; } = string.Empty;

        public string? City { get; set; }

        public int? PostCode { get; set; }
    }

    public class PasswordInputModel
    {
        [Required(ErrorMessage = "FieldRequired"), DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired"), DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired"), DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "PasswordsDoNotMatch")]
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }

    public void OnGet()
    {
        Input = new InputModel
        {
            UserName = CurrentUser.UserName,
            FirstName = CurrentUser.FirstName,
            LastName = CurrentUser.LastName,
            Email = CurrentUser.Email ?? string.Empty,
            City = CurrentUser.City,
            PostCode = CurrentUser.PostCode
        };
    }

    // Two independent forms share this page (profile fields, change password) - each POST only
    // fills in one of Input/PasswordInput, so the other's [Required] properties would otherwise
    // fail bind-time validation and block submission. Clear that and validate only the model the
    // handler that actually ran cares about.
    public async Task<IActionResult> OnPostAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(Input, nameof(Input)))
            return Page();

        if (Input.UserName != CurrentUser.UserName)
        {
            var userNameResult = await Users.SetUserNameAsync(CurrentUser, Input.UserName);
            if (!userNameResult.Succeeded)
            {
                foreach (var error in userNameResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return Page();
            }
        }

        if (!string.Equals(Input.Email, CurrentUser.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await Users.SetEmailAsync(CurrentUser, Input.Email);
            if (!emailResult.Succeeded)
            {
                foreach (var error in emailResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);

                return Page();
            }
        }

        CurrentUser.FirstName = Input.FirstName;
        CurrentUser.LastName = Input.LastName;
        CurrentUser.City = Input.City;
        CurrentUser.PostCode = Input.PostCode;

        var result = await Users.UpdateAsync(CurrentUser);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }

        return RedirectToPage();
    }

    public async Task<IActionResult> OnPostChangePasswordAsync()
    {
        ModelState.Clear();
        if (!TryValidateModel(PasswordInput, nameof(PasswordInput)))
            return Page();

        var result = await Users.ChangePasswordAsync(CurrentUser, PasswordInput.CurrentPassword, PasswordInput.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            return Page();
        }

        Logger.LogInformation("User {UserId} changed their own password", CurrentUser.Id);
        return RedirectToPage();
    }
}
