using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MangaWhisper.Application.Services;

namespace MangaWhisper.Infrastructure.Services;

public class ChapterCheckingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChapterCheckingBackgroundService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(4);

    public ChapterCheckingBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ChapterCheckingBackgroundService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Chapter Checking Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CheckAllActiveCheckersAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Chapter checking was cancelled");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while checking for new chapters");
            }

            try
            {
                await Task.Delay(_checkInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Chapter Checking Background Service stopped");
    }

    private async Task CheckAllActiveCheckersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var checkingService = scope.ServiceProvider.GetRequiredService<IChapterCheckingService>();

        await checkingService.CheckAllActiveCheckersManuallyAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Chapter Checking Background Service is stopping");
        await base.StopAsync(cancellationToken);
    }
}
