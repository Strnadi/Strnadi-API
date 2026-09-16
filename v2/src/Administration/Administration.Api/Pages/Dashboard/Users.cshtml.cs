using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Platform.Shared.Kernel.Authorization;

namespace Administration.Api.Pages.Dashboard;

public record UserListItem(Guid Id, string FirstName, string LastName, DateTime CreatedAt, bool Deleted, bool Legacy, bool EmailConfirmed, string? Email, string? City, int? PostCode);

public record UsersTableViewModel(IReadOnlyList<UserListItem> Users, bool CanViewUsersConfidential, bool CanManageUsers, string? Search, string SearchField);

public class UsersModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions, ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    [BindProperty(SupportsGet = true)]
    public string? Search { get; set; }

    [BindProperty(SupportsGet = true)]
    public string SearchField { get; set; } = "name";

    public IReadOnlyList<UserListItem> UserList { get; private set; } = [];

    public async Task<IActionResult> OnGetAsync()
    {
        if (!CanViewUsersBasic)
            return RequirePermission(false, Permissions.ViewUsersBasic);

        var query = Db.Users.AsQueryable();

        var term = Search?.Trim();
        if (!string.IsNullOrEmpty(term))
        {
            // Email is only ever shown to CanViewUsersConfidential - matching against it for a
            // basic-view-only caller would let them fish for whether an email is registered even
            // though they can never see it in the results, so "email" falls back to a name search
            // for anyone without that permission instead of being honored.
            query = SearchField == "email" && CanViewUsersConfidential
                ? query.Where(u => u.Email != null && EF.Functions.ILike(u.Email, $"%{term}%"))
                : query.Where(u =>
                    EF.Functions.ILike(u.FirstName, $"%{term}%") ||
                    EF.Functions.ILike(u.LastName, $"%{term}%"));
        }

        UserList = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Select(u => new UserListItem(u.Id, u.FirstName, u.LastName, u.CreatedAt, u.Deleted, u.Legacy, u.EmailConfirmed, u.Email, u.City, u.PostCode))
            .ToListAsync();

        return Page();
    }
}
