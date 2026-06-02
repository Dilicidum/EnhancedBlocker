using Microsoft.EntityFrameworkCore;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;
using EnhancedBlocker.Infrastructure.Persistence;

namespace EnhancedBlocker.Infrastructure.Repositories;

public sealed class CategoryDomainCache(AppDbContext db) : ICategoryDomainCache
{
    public Task<CategoryDomain?> GetAsync(string domain, CancellationToken ct)
    {
        var normalized = domain.Trim().ToLowerInvariant();
        return db.CategoryDomains
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.Domain == normalized, ct);
    }
}
