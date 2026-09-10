using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Administration.Api.Pages.Dashboard;

public class UsersModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions)
    : DashboardPageModel(users, db, permissions)
{
    public IActionResult OnGet() => CanManageUsers ? Page() : NotFound();
}
