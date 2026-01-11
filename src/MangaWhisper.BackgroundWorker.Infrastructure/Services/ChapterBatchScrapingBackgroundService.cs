using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MangaWhisper.Application.Services;

namespace MangaWhisper.Infrastructure.Services;

/// <summary>
/// Background service that scrapes all available chapters for all active checkers
/// and then stops. This is a one-time batch operation, not a continuous service.
/// </summary>
public class ChapterBatchScrapingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChapterBatchScrapingBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly IHostApplicationLifetime _applicationLifetime;
    private readonly bool _enableBatchScraping;
    private readonly bool _stopApplicationAfterCompletion;

    public ChapterBatchScrapingBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ChapterBatchScrapingBackgroundService> logger,
        IConfiguration configuration,
        IHostApplicationLifetime applicationLifetime)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        _applicationLifetime = applicationLifetime;
        
        // Parse configuration
        _enableBatchScraping = bool.Parse(_configuration["ENABLE_BATCH_SCRAPING"] ?? "false");
        _stopApplicationAfterCompletion = bool.Parse(_configuration["STOP_APP_AFTER_BATCH_SCRAPING"] ?? "false");
        
        _logger.LogInformation(
            "Batch Scraping Service configured: Enabled={Enabled}, StopAfterCompletion={StopAfter}",
            _enableBatchScraping,
            _stopApplicationAfterCompletion);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Only run if batch scraping is enabled
        if (!_enableBatchScraping)
        {
            _logger.LogInformation("Batch scraping is disabled. Set ENABLE_BATCH_SCRAPING=true to enable.");
            return;
        }

        _logger.LogInformation("=== Chapter Batch Scraping Service Started ===");
        _logger.LogInformation("This service will scrape all available chapters for all active checkers and then stop.");

        try
        {
            await ScrapAllAvailableChaptersAsync(stoppingToken);
            
            _logger.LogInformation("=== Chapter Batch Scraping Service Completed Successfully ===");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Batch scraping was cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error occurred during batch scraping");
        }
        finally
        {
            // Stop the application if configured to do so
            if (_stopApplicationAfterCompletion)
            {
                _logger.LogInformation("Stopping application as configured (STOP_APP_AFTER_BATCH_SCRAPING=true)");
                _applicationLifetime.StopApplication();
            }
        }
    }

    private async Task ScrapAllAvailableChaptersAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var checkingService = scope.ServiceProvider.GetRequiredService<IChapterCheckingService>();

        _logger.LogInformation("Retrieving all active checkers for batch scraping...");
        
        var activeCheckers = await checkingService.GetActiveCheckersAsync();
        var checkersList = activeCheckers.ToList();
        
        _logger.LogInformation("Found {Count} active checkers to process", checkersList.Count);

        if (checkersList.Count == 0)
        {
            _logger.LogWarning("No active checkers found. Nothing to scrape.");
            return;
        }

        int totalChaptersFound = 0;
        int successfulCheckers = 0;
        int failedCheckers = 0;

        for (int i = 0; i < checkersList.Count; i++)
        {
            var checker = checkersList[i];
            
            if (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning("Batch scraping cancelled after processing {Completed}/{Total} checkers", 
                    i, checkersList.Count);
                break;
            }

            try
            {
                _logger.LogInformation(
                    "[{Current}/{Total}] Processing manga: {MangaTitle} (Checker ID: {CheckerId})",
                    i + 1, 
                    checkersList.Count, 
                    checker.Manga?.Title ?? "Unknown",
                    checker.Id);

                var chapters = await checkingService.ProcessAllAvailableChaptersForCheckerAsync(
                    checker.Id, 
                    cancellationToken);

                if (chapters.Count > 0)
                {
                    totalChaptersFound += chapters.Count;
                    successfulCheckers++;
                    
                    _logger.LogInformation(
                        "Successfully processed {ChapterCount} chapter(s) for manga: {MangaTitle}",
                        chapters.Count,
                        checker.Manga?.Title ?? "Unknown");
                }
                else
                {
                    _logger.LogInformation(
                        "No new chapters found for manga: {MangaTitle}",
                        checker.Manga?.Title ?? "Unknown");
                    successfulCheckers++;
                }
            }
            catch (Exception ex)
            {
                failedCheckers++;
                _logger.LogError(ex, 
                    "Failed to process manga: {MangaTitle} (Checker ID: {CheckerId})",
                    checker.Manga?.Title ?? "Unknown",
                    checker.Id);
            }
        }

        // Final summary
        _logger.LogInformation("");
        _logger.LogInformation("=== Batch Scraping Summary ===");
        _logger.LogInformation("Total Checkers Processed: {Total}", checkersList.Count);
        _logger.LogInformation("Successful: {Success}", successfulCheckers);
        _logger.LogInformation("Failed: {Failed}", failedCheckers);
        _logger.LogInformation("Total Chapters Found: {Chapters}", totalChaptersFound);
        _logger.LogInformation("==============================");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Chapter Batch Scraping Service is stopping");
        await base.StopAsync(cancellationToken);
    }
}
