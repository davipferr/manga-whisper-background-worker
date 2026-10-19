namespace MangaWhisper.Domain.Repositories;

public interface IWorkerHeartbeatRepository
{
    Task BeatAsync(DateTime startedAtUtc, CancellationToken cancellationToken = default);
}
