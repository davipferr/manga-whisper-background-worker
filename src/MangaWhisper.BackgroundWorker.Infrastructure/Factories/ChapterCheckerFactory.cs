using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Chrome;
using MangaWhisper.Domain.Interfaces;
using MangaWhisper.Domain.Services;
using MangaWhisper.Domain.Factories;
using System.Collections.Concurrent;

namespace MangaWhisper.Infrastructure.Factories;

public class ChapterCheckerFactory : IChapterCheckerFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, IWebDriver> _webDrivers = new();
    private bool _disposed = false;

    public ChapterCheckerFactory(
        IServiceProvider serviceProvider,
        ILoggerFactory loggerFactory,
        IHttpClientFactory httpClientFactory)
    {
        _serviceProvider = serviceProvider;
        _loggerFactory = loggerFactory;
        _httpClientFactory = httpClientFactory;
    }

    public IChapterChecker CreateChecker(string siteIdentifier)
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(ChapterCheckerFactory));

        var webDriver = GetOrCreateWebDriver(siteIdentifier);
        var httpClient = _httpClientFactory.CreateClient();

        return siteIdentifier.ToLowerInvariant() switch
        {
            "mugiwara-oficial" => new MugiwaraOficialChecker(
                webDriver,
                httpClient,
                _loggerFactory.CreateLogger<MugiwaraOficialChecker>()),

            _ => throw new NotSupportedException($"Site identifier '{siteIdentifier}' is not supported")
        };
    }

    public IEnumerable<string> GetAvailableSites()
    {
        return new[]
        {
            "mugiwara-oficial",
        };
    }

    //TODO: Improve WebDriver management (e.g., pooling, reusing, etc.)
    //TODO: Move the WebDriver creation logic to a dedicated factory/service
    private IWebDriver GetOrCreateWebDriver(string siteIdentifier)
    {
        return _webDrivers.GetOrAdd(siteIdentifier, _ => CreateWebDriver());
    }

    private IWebDriver CreateWebDriver()
    {
        var chromeOptions = new ChromeOptions();
        chromeOptions.AddArgument("--headless");
        chromeOptions.AddArgument("--disable-gpu");
        chromeOptions.AddArgument("--window-size=1920,1080");
        chromeOptions.AddArgument("--no-sandbox");
        chromeOptions.AddArgument("--disable-dev-shm-usage");

        return new ChromeDriver(chromeOptions);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed && disposing)
        {
            foreach (var webDriver in _webDrivers.Values)
            {
                try
                {
                    webDriver?.Quit();
                    webDriver?.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error disposing WebDriver: {ex.Message}");
                }
            }
            _webDrivers.Clear();
            _disposed = true;
        }
    }
}
