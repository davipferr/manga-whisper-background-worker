using Microsoft.EntityFrameworkCore;
using MangaWhisper.Domain.Entities;
using MangaWhisper.Domain.Repositories;
using MangaWhisper.Infrastructure.Data;

namespace MangaWhisper.Infrastructure.Repositories;

public class ScrapingJobRepository : IScrapingJobRepository
{
    private readonly ApplicationDbContext _context;

    public ScrapingJobRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<List<ScrapingJob>> GetEnabledAsync(CancellationToken cancellationToken = default)
    {
        return await _context.ScrapingJobs
            .Where(j => j.IsEnabled)
            .ToListAsync(cancellationToken);
    }

    public async Task<List<ScrapingJob>> GetDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        return await _context.ScrapingJobs
            .Include(j => j.MangaChecker)
            .Where(j => j.IsEnabled && j.NextRunAt != null && j.NextRunAt <= nowUtc)
            .OrderBy(j => j.NextRunAt)
            .ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
