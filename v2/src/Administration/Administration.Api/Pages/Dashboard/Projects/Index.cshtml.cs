using Administration.Api.Pages.Dashboard;
using Administration.Domain.Authorization;
using Administration.Domain.Entities;
using Administration.Domain.Persistence.Repositories;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Administration.Api.Pages.Dashboard.Projects;

public class IndexModel(UserManager<User> users, AdminDbContext db, IUserPermissionsRepository permissions, ILogger<DashboardPageModel> logger)
    : DashboardPageModel(users, db, permissions, logger)
{
    public IActionResult OnGet() => RequirePermission(CanManageProjects, Permissions.ManageProjects);
}
