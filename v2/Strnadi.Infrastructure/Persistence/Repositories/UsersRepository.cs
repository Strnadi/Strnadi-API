using Microsoft.EntityFrameworkCore;
using Strnadi.Domain.Entities;
using Strnadi.Domain.Persistence.Repositories;

namespace Strnadi.Infrastructure.Persistence.Repositories;

public class UsersRepository(AppDbContext db) : IUsersRepository
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

    public void Update(User user)
    {
        db.Users.Update(user);
    }

    public void Remove(User user)
    {
        db.Users.Remove(user);
    }
}