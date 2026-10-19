using Microsoft.EntityFrameworkCore;
using MangaWhisper.Domain.Entities;
using MangaWhisper.Domain.Repositories;
using MangaWhisper.Infrastructure.Data;

namespace MangaWhisper.Infrastructure.Repositories;

public class WorkerHeartbeatRepository : IWorkerHeartbeatRepository
{
    private readonly ApplicationDbContext _context;

    public WorkerHeartbeatRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task BeatAsync(DateTime startedAtUtc, CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO "WorkerHeartbeats" ("Id", "LastSeenAt", "StartedAt")
            VALUES ({WorkerHeartbeat.SingletonId}, {DateTime.UtcNow}, {startedAtUtc})
            ON CONFLICT ("Id") DO UPDATE SET "LastSeenAt" = EXCLUDED."LastSeenAt", "StartedAt" = EXCLUDED."StartedAt"
            """, cancellationToken);
    }
}
