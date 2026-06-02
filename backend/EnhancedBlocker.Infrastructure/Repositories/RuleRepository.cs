using Microsoft.EntityFrameworkCore;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;
using EnhancedBlocker.Infrastructure.Persistence;

namespace EnhancedBlocker.Infrastructure.Repositories;

public sealed class RuleRepository(AppDbContext db) : IRuleRepository
{
    public async Task<IReadOnlyList<Rule>> ListAsync(CancellationToken ct) =>
        await db.Rules.AsNoTracking().OrderBy(r => r.Pattern).ToListAsync(ct);

    public Task<Rule?> GetAsync(Guid id, CancellationToken ct) =>
        db.Rules.FirstOrDefaultAsync(r => r.Id == id, ct);

    public async Task AddAsync(Rule rule, CancellationToken ct)
    {
        db.Rules.Add(rule);
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var rule = await db.Rules.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (rule is null)
            return false;

        db.Rules.Remove(rule);
        await db.SaveChangesAsync(ct);
        return true;
    }
}
