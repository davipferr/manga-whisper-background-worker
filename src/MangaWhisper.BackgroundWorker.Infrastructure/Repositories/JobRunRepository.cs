using Microsoft.EntityFrameworkCore;
using MangaWhisper.Common.Enums;
using MangaWhisper.Domain.Entities;
using MangaWhisper.Domain.Repositories;
using MangaWhisper.Infrastructure.Data;

namespace MangaWhisper.Infrastructure.Repositories;

public class JobRunRepository : IJobRunRepository
{
    private readonly ApplicationDbContext _context;

    public JobRunRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<JobRun>> GetByStatusAsync(JobRunStatus status, CancellationToken cancellationToken = default)
    {
        return await _context.JobRuns
            .Where(r => r.Status == status)
            .ToListAsync(cancellationToken);
    }

    public async Task<JobRun?> GetNextPendingAsync(CancellationToken cancellationToken = default)
    {
        return await _context.JobRuns
            .Where(r => r.Status == JobRunStatus.Pending)
            .OrderBy(r => r.RequestedAt)
            .ThenBy(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<bool> HasOpenRunForCheckerAsync(int mangaCheckerId, CancellationToken cancellationToken = default)
    {
        return await _context.JobRuns
            .AnyAsync(r => r.MangaCheckerId == mangaCheckerId
                && (r.Status == JobRunStatus.Pending || r.Status == JobRunStatus.Running), cancellationToken);
    }

    public async Task AddAsync(JobRun run, CancellationToken cancellationToken = default)
    {
        await _context.JobRuns.AddAsync(run, cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
