using Administration.Api.Pages.Dashboard;
using Administration.Domain.Entities;
using Administration.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;

namespace Administration.Api.Pages.Dashboard.Projects;

public class IndexModel(UserManager<User> users, AdminDbContext db) : DashboardPageModel(users, db)
{
    public void OnGet()
    {
    }
}
