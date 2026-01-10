using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using MangaWhisper.Domain.Entities;
using MangaWhisper.Domain.Interfaces;

namespace MangaWhisper.Domain.Services;

public abstract class BaseChapterChecker : IChapterChecker
{
    protected readonly IWebDriver webDriver;
    protected readonly string siteName;
    protected readonly ILogger logger;
    protected readonly HttpClient httpClient;

    public abstract string SiteIdentifier { get; }
    protected virtual bool RequiresSelenium => false;
    protected abstract string chapterUrlPattern { get; }

    protected BaseChapterChecker(IWebDriver webDriver, HttpClient httpClient, ILogger logger)
    {
        this.webDriver = webDriver;
        this.httpClient = httpClient;
        this.logger = logger;
        siteName = GetSiteName();
    }

    protected abstract string GetSiteName();
    protected abstract string BuildChapterUrl(int chapterNumber);
    protected abstract Task<bool> CheckChapterExistsViaSeleniumRules(string url);
    protected abstract Task<Chapter?> ExtractNewChapterInfoRulesAsync(string url, int mangaId);

    private async Task<bool> CheckChapterExistsViaHttp(string url)
    {
        try
        {
            using var response = await httpClient.GetAsync(url);

            if (response.IsSuccessStatusCode)
            {
                return true;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                return false;
            }

            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking chapter via HTTP: {Url}", url);
            return false;
        }
    }

    private async Task<bool> CheckChapterExistsViaSelenium(string url)
    {
        try
        {
            return await CheckChapterExistsViaSeleniumRules(url);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking chapter via Selenium: {Url}", url);
            return false;
        }
    }

    public async Task<bool> HasNewChapterAsync(MangaChecker checker)
    {
        try
        {
            var expectedChapterNumber = checker.GetExpectedNextChapter();
            var chapterUrl = BuildChapterUrl(expectedChapterNumber);

            // Check if this scraper requires Selenium
            if (!RequiresSelenium)
            {
                // Use HTTP-only check
                var httpResult = await CheckChapterExistsViaHttp(chapterUrl);
                return httpResult;
            }

            return await CheckChapterExistsViaSelenium(chapterUrl);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error checking for new chapter for subscription {SubscriptionId}", checker.Id);
            return false;
        }
    }

    public async Task<Chapter?> ExtractNewChapterInfoAsync(MangaChecker checker)
    {
        try
        {
            var nextChapter = checker.GetExpectedNextChapter();
            var chapterUrl = BuildChapterUrl(nextChapter);

            if (string.IsNullOrEmpty(chapterUrl))
            {
                logger.LogWarning("Could not build chapter URL for chapter {ChapterNumber}", nextChapter);
                return null;
            }

            var newChapter = await ExtractNewChapterInfoRulesAsync(chapterUrl, checker.MangaId);

            if (newChapter == null)
            {
                logger.LogWarning("Failed to extract chapter information from {SiteName} for URL: {Url}", GetSiteName(), chapterUrl);
                return null;
            }

            return newChapter;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error extracting chapter information from {SiteName}", GetSiteName());
            return null;
        }
    }
}
