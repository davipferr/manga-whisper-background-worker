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
            INSERT INTO "WorkerHeartbeats" ("Id", "LastSeenAt", "StartedAt", "StoppedAt")
            VALUES ({WorkerHeartbeat.SingletonId}, {DateTime.UtcNow}, {startedAtUtc}, NULL)
            ON CONFLICT ("Id") DO UPDATE SET "LastSeenAt" = EXCLUDED."LastSeenAt", "StartedAt" = EXCLUDED."StartedAt", "StoppedAt" = NULL
            """, cancellationToken);
    }

    public async Task MarkStoppedAsync(CancellationToken cancellationToken = default)
    {
        await _context.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "WorkerHeartbeats" SET "StoppedAt" = {DateTime.UtcNow} WHERE "Id" = {WorkerHeartbeat.SingletonId}
            """, cancellationToken);
    }
}
