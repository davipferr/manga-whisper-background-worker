using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MangaWhisper.Application.Services;

namespace MangaWhisper.Infrastructure.Services;

public class ChapterCheckingBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ChapterCheckingBackgroundService> _logger;
    private readonly IConfiguration _configuration;
    private readonly TimeZoneInfo _brtTimeZone;
    private readonly HashSet<DayOfWeek> _allowedDays;
    private readonly int _startHour;
    private readonly int _checkIntervalHours;

    public ChapterCheckingBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<ChapterCheckingBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _configuration = configuration;
        
        // Initialize BRT timezone (UTC-3)
        _brtTimeZone = TimeZoneInfo.FindSystemTimeZoneById("E. South America Standard Time");
        
        // Parse allowed days from configuration
        var daysConfig = _configuration["CHECK_DAYS_OF_WEEK"] ?? "0,1,2,3,4,5,6";
        _allowedDays = daysConfig.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(d => (DayOfWeek)int.Parse(d.Trim()))
            .ToHashSet();
        
        // Parse start hour and interval
        _startHour = int.Parse(_configuration["CHECK_START_HOUR"] ?? "8");
        _checkIntervalHours = int.Parse(_configuration["CHECK_INTERVAL_HOURS"] ?? "4");
        
        _logger.LogInformation(
            "Background service configured: Days={Days}, StartHour={StartHour}h BRT, Interval={Interval}h",
            string.Join(", ", _allowedDays.OrderBy(d => d)),
            _startHour,
            _checkIntervalHours);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Chapter Checking Background Service started");

        while (!stoppingToken.IsCancellationRequested)
        {
            var nowBrt = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _brtTimeZone);
            
            // Check if we should run now
            if (ShouldRunNow(nowBrt))
            {
                try
                {
                    _logger.LogInformation("Starting chapter check at {Time} BRT", nowBrt.ToString("yyyy-MM-dd HH:mm:ss"));
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
            }
            else
            {
                _logger.LogDebug("Skipping check - current time {Time} BRT doesn't match schedule", nowBrt.ToString("yyyy-MM-dd HH:mm:ss"));
            }

            // Calculate next run time
            var nextRunTime = CalculateNextRunTime(nowBrt);
            var delay = nextRunTime - nowBrt;
            
            if (delay.TotalMilliseconds > 0)
            {
                _logger.LogInformation(
                    "Next check scheduled for {NextRun} BRT (in {Hours}h {Minutes}m)",
                    nextRunTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    (int)delay.TotalHours,
                    delay.Minutes);
                
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }

        _logger.LogInformation("Chapter Checking Background Service stopped");
    }

    private bool ShouldRunNow(DateTime nowBrt)
    {
        // Check if today is an allowed day
        if (!_allowedDays.Contains(nowBrt.DayOfWeek))
        {
            return false;
        }
        
        // Check if we're at or past the start hour
        return nowBrt.Hour >= _startHour;
    }
    
    private DateTime CalculateNextRunTime(DateTime currentBrt)
    {
        var nextRun = currentBrt.AddHours(_checkIntervalHours);
        
        // If next run is on a different day, check if that day is allowed
        while (!_allowedDays.Contains(nextRun.DayOfWeek) || nextRun.Hour < _startHour)
        {
            if (!_allowedDays.Contains(nextRun.DayOfWeek))
            {
                // Move to next day at start hour
                nextRun = nextRun.Date.AddDays(1).AddHours(_startHour);
            }
            else if (nextRun.Hour < _startHour)
            {
                // Move to start hour today
                nextRun = nextRun.Date.AddHours(_startHour);
            }
        }
        
        return nextRun;
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
