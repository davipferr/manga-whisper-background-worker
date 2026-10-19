using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MangaWhisper.Application.Services;

namespace MangaWhisper.Infrastructure.Services;

/// <summary>
/// Executes the scraping jobs and "Run now" requests the admin creates through the API.
/// The worker has no schedule of its own: every <see cref="PollInterval"/> it queues the jobs that
/// are due and then executes the Pending runs one at a time.
/// </summary>
public class JobRunnerBackgroundService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<JobRunnerBackgroundService> _logger;

    public JobRunnerBackgroundService(IServiceProvider serviceProvider, ILogger<JobRunnerBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Job runner started; polling every {Seconds}s", PollInterval.TotalSeconds);

        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<IJobRunnerService>();
                await runner.PrepareForStartupAsync(stoppingToken);
            }

            using var timer = new PeriodicTimer(PollInterval);

            do
            {
                await PollAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }

        _logger.LogInformation("Job runner stopped");
    }

    private async Task PollAsync(CancellationToken stoppingToken)
    {
        try
        {
            using (var scope = _serviceProvider.CreateScope())
            {
                var runner = scope.ServiceProvider.GetRequiredService<IJobRunnerService>();
                await runner.ScheduleDueJobsAsync(stoppingToken);
            }

            // A fresh scope (and DbContext) per run, so a long backfill doesn't share tracked entities with the next run
            bool executed;
            do
            {
                using var scope = _serviceProvider.CreateScope();
                var runner = scope.ServiceProvider.GetRequiredService<IJobRunnerService>();
                executed = await runner.ExecuteNextPendingRunAsync(stoppingToken);
            }
            while (executed && !stoppingToken.IsCancellationRequested);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while polling for scraping jobs");
        }
    }
}
