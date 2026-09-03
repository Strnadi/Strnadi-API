using Microsoft.EntityFrameworkCore;
using Tenant.Domain.Entities;
using Tenant.Domain.Persistence.Repositories;

namespace Tenant.Infrastructure.Persistence.Repositories;

public class UsersRepository(TenantDbContext db) : IUsersRepository
{
    public async Task<User[]> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await db.Users.ToArrayAsync(cancellationToken);
    }
    
    public async Task<User?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default)
    {
        return await db.Users.AnyAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<bool> ExistsAsync(string email, CancellationToken cancellationToken = default)
    {
        return await db.Users.AnyAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetByGoogleIdAsync(string googleId, CancellationToken cancellationToken = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.GoogleId == googleId, cancellationToken);
    }

    public async Task<User?> GetByAppleIdAsync(string appleId, CancellationToken cancellationToken = default)
    {
        return await db.Users.FirstOrDefaultAsync(u => u.Appleid == appleId, cancellationToken);
    }

    public void Add(User user)
    {
        db.Users.Add(user);
    }

    public void Update(User user)
    {
        db.Users.Update(user);
    }

    public void Remove(User user)
    {
        db.Users.Remove(user);
    }
}