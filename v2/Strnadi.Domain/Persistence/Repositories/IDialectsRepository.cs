using Strnadi.Domain.Entities;

namespace Strnadi.Domain.Persistence.Repositories;

public interface IDialectsRepository
{
    Task<Dialect[]> GetAllAsync(CancellationToken cancellationToken = default);
    
    Task<Dialect?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
}