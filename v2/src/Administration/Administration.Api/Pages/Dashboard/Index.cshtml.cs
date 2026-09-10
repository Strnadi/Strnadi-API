using System.ComponentModel.DataAnnotations;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Administration.Api.Pages.Dashboard;

public class IndexModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions)
    : DashboardPageModel(users, db, permissions)
{
    [BindProperty]
    public InputModel Input { get; set; } = new();

    public class InputModel
    {
        public string? UserName { get; set; }

        [Required(ErrorMessage = "FieldRequired")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "FieldRequired")]
        public string LastName { get; set; } = string.Empty;

        public string? City { get; set; }

        public int? PostCode { get; set; }
    }

    public void OnGet()
    {
        Input = new InputModel
        {
            UserName = CurrentUser.UserName,
            FirstName = CurrentUser.FirstName,
            LastName = CurrentUser.LastName,
            City = CurrentUser.City,
            PostCode = CurrentUser.PostCode
        };
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
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
}
