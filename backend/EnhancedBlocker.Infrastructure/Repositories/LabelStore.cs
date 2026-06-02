using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;
using EnhancedBlocker.Infrastructure.Persistence;

namespace EnhancedBlocker.Infrastructure.Repositories;

public sealed class LabelStore(AppDbContext db) : ILabelStore
{
    public async Task AddAsync(Label label, CancellationToken ct)
    {
        db.Labels.Add(label);
        await db.SaveChangesAsync(ct);
    }
}
