using System.ComponentModel.DataAnnotations;
using Administration.Domain.Authorization;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Administration.Api.Pages.Dashboard.Users;

public class EditModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions, ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    public User TargetUser { get; private set; } = null!;

    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        [Required(ErrorMessage = "FieldRequired")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired")]
        public string LastName { get; set; } = string.Empty;

        public string? UserName { get; set; }

        [Required(ErrorMessage = "FieldRequired"), EmailAddress(ErrorMessage = "EmailInvalid")]
        public string Email { get; set; } = string.Empty;

        public string? City { get; set; }

        public int? PostCode { get; set; }

        // Set-only: never populated from the existing user, only ever written when non-empty.
        [DataType(DataType.Password)]
        public string? NewPassword { get; set; }

        [DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "PasswordsDoNotMatch")]
        public string? ConfirmNewPassword { get; set; }
    }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        if (!CanManageUsers)
            return RequirePermission(false, Permissions.ManageUsers);

        var user = await Users.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        TargetUser = user;
        Input = new InputModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserName = user.UserName,
            Email = user.Email ?? string.Empty,
            City = user.City,
            PostCode = user.PostCode
        };

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(Guid id)
    {
        if (!CanManageUsers)
            return RequirePermission(false, Permissions.ManageUsers);

        var user = await Users.FindByIdAsync(id.ToString());
        if (user is null)
            return NotFound();

        TargetUser = user;

        if (!ModelState.IsValid)
            return Page();

        var newUserName = string.IsNullOrWhiteSpace(Input.UserName) ? null : Input.UserName;
        if (!string.Equals(newUserName, user.UserName, StringComparison.Ordinal))
        {
            var userNameResult = await Users.SetUserNameAsync(user, newUserName);
            if (!userNameResult.Succeeded)
            {
                foreach (var error in userNameResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }
        }

        if (!string.Equals(Input.Email, user.Email, StringComparison.OrdinalIgnoreCase))
        {
            var emailResult = await Users.SetEmailAsync(user, Input.Email);
            if (!emailResult.Succeeded)
            {
                foreach (var error in emailResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }
        }

        user.FirstName = Input.FirstName;
        user.LastName = Input.LastName;
        user.City = Input.City;
        user.PostCode = Input.PostCode;

        var updateResult = await Users.UpdateAsync(user);
        if (!updateResult.Succeeded)
        {
            foreach (var error in updateResult.Errors)
                ModelState.AddModelError(string.Empty, error.Description);
            return Page();
        }

        if (!string.IsNullOrEmpty(Input.NewPassword))
        {
            var token = await Users.GeneratePasswordResetTokenAsync(user);
            var passwordResult = await Users.ResetPasswordAsync(user, token, Input.NewPassword);
            if (!passwordResult.Succeeded)
            {
                foreach (var error in passwordResult.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
                return Page();
            }
        }

        Logger.LogInformation("Admin {AdminId} updated user {UserId}", CurrentUser.Id, user.Id);
        return RedirectToPage("/Dashboard/Users");
    }
}
