using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;
using EnhancedBlocker.Infrastructure.Persistence;

namespace EnhancedBlocker.Infrastructure.Repositories;

public sealed class EventStore(AppDbContext db) : IEventStore
{
    public async Task AddRangeAsync(IEnumerable<Event> events, CancellationToken ct)
    {
        await db.Events.AddRangeAsync(events, ct);
        await db.SaveChangesAsync(ct);
    }
}
