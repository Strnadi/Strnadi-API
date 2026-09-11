using Administration.Domain.Authorization;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Administration.Api.Pages.Dashboard;

public record UserListItem(Guid Id, string FirstName, string LastName, DateTime CreatedAt, bool Deleted, bool Legacy, bool EmailConfirmed, string? Email, string? City, int? PostCode);

public class UsersModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions, ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    public IReadOnlyList<UserListItem> UserList { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!CanViewUsersBasic)
            return RequirePermission(false, Permissions.ViewUsersBasic);

        UserList = await Db.Users
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new UserListItem(u.Id, u.FirstName, u.LastName, u.CreatedAt, u.Deleted, u.Legacy, u.EmailConfirmed, u.Email, u.City, u.PostCode))
            .ToListAsync();

        return Page();
    }
}
