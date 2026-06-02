using Microsoft.EntityFrameworkCore;
using EnhancedBlocker.Application.Ports;
using EnhancedBlocker.Domain;
using EnhancedBlocker.Infrastructure.Persistence;

namespace EnhancedBlocker.Infrastructure.Repositories;

public sealed class FocusSessionRepository(AppDbContext db) : IFocusSessionRepository
{
    public async Task AddAsync(FocusSession session, CancellationToken ct)
    {
        db.FocusSessions.Add(session);
        await db.SaveChangesAsync(ct);
    }

    public Task<FocusSession?> GetAsync(Guid id, CancellationToken ct) =>
        db.FocusSessions.FirstOrDefaultAsync(s => s.Id == id, ct);

    public Task<FocusSession?> GetActiveAsync(CancellationToken ct) =>
        db.FocusSessions
            .Where(s => s.EndedAt == null)
            .OrderByDescending(s => s.StartedAt)
            .FirstOrDefaultAsync(ct);

    public async Task UpdateAsync(FocusSession session, CancellationToken ct)
    {
        // session is already tracked (loaded via GetAsync/GetActiveAsync); persist its changes.
        await db.SaveChangesAsync(ct);
    }
}
