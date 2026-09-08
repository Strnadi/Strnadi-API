using Administration.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Administration.Infrastructure.Identity;

public static class UserManagerExtensions
{
    extension(UserManager<User> userManager)
    {
        public async Task<bool> IsActiveAsync(string userId)
        {
            var user = await userManager.FindByIdAsync(userId);
            return user is not null && !user.Deleted;
        }
    }
}