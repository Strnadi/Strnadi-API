using Administration.Api.Pages.Dashboard;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Administration.Api.Pages.Dashboard.Projects;

public class IndexModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions)
    : DashboardPageModel(users, db, permissions)
{
    public IActionResult OnGet() => CanManageProjects ? Page() : NotFound();
}
