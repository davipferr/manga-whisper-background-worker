using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MangaWhisper.Domain.Repositories;

namespace MangaWhisper.Infrastructure.Services;

/// <summary>
/// Writes "I'm alive" to the database so the admin panel can show whether the worker is running.
/// Separate from the job runner so a long backfill doesn't make the worker look offline.
/// </summary>
public class WorkerHeartbeatBackgroundService : BackgroundService
{
    private static readonly TimeSpan BeatInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkerHeartbeatBackgroundService> _logger;
    private readonly DateTime _startedAtUtc = DateTime.UtcNow;

    public WorkerHeartbeatBackgroundService(IServiceProvider serviceProvider, ILogger<WorkerHeartbeatBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            using var timer = new PeriodicTimer(BeatInterval);

            do
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var repository = scope.ServiceProvider.GetRequiredService<IWorkerHeartbeatRepository>();
                    await repository.BeatAsync(_startedAtUtc, stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogWarning(ex, "Failed to write the worker heartbeat");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    // On a clean shutdown (docker compose stop/down) the admin panel shows the worker as stopped right away,
    // instead of waiting for the heartbeat to go stale
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IWorkerHeartbeatRepository>();
            await repository.MarkStoppedAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to record the worker shutdown");
        }
    }
}
