using Administration.Domain.Entities;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Administration.Api.Pages.Dashboard;

public class UsersModel(UserManager<User> users, AdminDbContext db) : DashboardPageModel(users, db)
{
    public IActionResult OnGet() => IsAdmin ? Page() : NotFound();
}
