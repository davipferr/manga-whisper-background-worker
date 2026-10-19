using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using MangaWhisper.Domain.Entities;
using MangaWhisper.Domain.Exceptions;
using MangaWhisper.Domain.Interfaces;

namespace MangaWhisper.Domain.Services;

/// <summary>
/// Base for the site scrapers. A "no" (chapter not released yet) is returned as false; any other problem
/// (site unreachable, browser error, page that doesn't look like what the scraper expects) is thrown as
/// <see cref="ChapterCheckException"/>, so a broken scraper fails the run instead of looking like "no news".
/// </summary>
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

    /// <returns>False when the site says the chapter is not released yet</returns>
    /// <exception cref="ChapterCheckException">The page is not a chapter nor a "not released" answer</exception>
    protected abstract Task<bool> CheckChapterExistsViaSeleniumRules(string url, int chapterNumber);

    /// <exception cref="ChapterCheckException">The page is not the expected chapter page</exception>
    protected abstract Task<Chapter> ExtractNewChapterInfoRulesAsync(string url, int chapterNumber, int mangaId);

    private async Task<bool> CheckChapterExistsViaHttp(string url)
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

        throw new ChapterCheckException($"{siteName} answered HTTP {(int)response.StatusCode} for {url}");
    }

    public async Task<bool> HasNewChapterAsync(MangaChecker checker)
    {
        var expectedChapterNumber = checker.GetExpectedNextChapter();
        var chapterUrl = BuildChapterUrl(expectedChapterNumber);

        try
        {
            // Check if this scraper don't requires Selenium to check chapter existence
            var exists = RequiresSelenium
                ? await CheckChapterExistsViaSeleniumRules(chapterUrl, expectedChapterNumber)
                : await CheckChapterExistsViaHttp(chapterUrl);

            logger.LogInformation("Chapter {ChapterNumber} on {SiteName}: {Result}",
                expectedChapterNumber, siteName, exists ? "found" : "not released yet");

            return exists;
        }
        catch (Exception ex) when (ex is not ChapterCheckException and not OperationCanceledException)
        {
            throw new ChapterCheckException(
                $"Could not check chapter {expectedChapterNumber} on {siteName}: {ex.Message}", ex);
        }
    }

    public async Task<Chapter> ExtractNewChapterInfoAsync(MangaChecker checker)
    {
        var nextChapter = checker.GetExpectedNextChapter();
        var chapterUrl = BuildChapterUrl(nextChapter);

        try
        {
            return await ExtractNewChapterInfoRulesAsync(chapterUrl, nextChapter, checker.MangaId);
        }
        catch (Exception ex) when (ex is not ChapterCheckException and not OperationCanceledException)
        {
            throw new ChapterCheckException(
                $"Could not read chapter {nextChapter} on {siteName}: {ex.Message}", ex);
        }
    }
}
