using MangaWhisper.Domain.Entities;

namespace MangaWhisper.Domain.Repositories;

public interface IScrapingJobRepository
{
    Task<List<ScrapingJob>> GetEnabledAsync(CancellationToken cancellationToken = default);
    Task<List<ScrapingJob>> GetDueAsync(DateTime nowUtc, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
