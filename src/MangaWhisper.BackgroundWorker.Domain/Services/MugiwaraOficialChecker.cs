using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using MangaWhisper.Domain.Entities;
using MangaWhisper.Domain.Exceptions;

namespace MangaWhisper.Domain.Services;

/// <summary>
/// mugiwarasoficial.org (the site is behind bot protection, so plain HTTP requests get 403 and Selenium is required).
/// How the site answers <c>/manga/one-piece/{n}</c>:
/// - released chapter: stays on that URL with the page title "One Piece — Capítulo {n}[: chapter title] · Mugiwaras Oficial"
/// - chapter not released yet: redirects to the manga page <c>/manga/one-piece</c>
/// - anything else (e.g. the "Página não encontrada" 404 page when the URL format changes) is an error
/// </summary>
public class MugiwaraOficialChecker : BaseChapterChecker
{
    private const string MangaPath = "/manga/one-piece";
    private const int ChapterTitleMaxLength = 255;
    private static readonly TimeSpan PageTimeout = TimeSpan.FromSeconds(20);

    public override string SiteIdentifier => "mugiwara-oficial";
    protected override string chapterUrlPattern => "https://mugiwarasoficial.org" + MangaPath + "/{1}";
    protected override bool RequiresSelenium => true;

    private enum ChapterPage
    {
        Released,
        NotReleased,
    }

    public MugiwaraOficialChecker(IWebDriver webDriver, HttpClient httpClient, ILogger<MugiwaraOficialChecker> logger)
        : base(webDriver, httpClient, logger)
    {
    }

    protected override string GetSiteName()
    {
        return "Mugiwara Oficial";
    }

    protected override string BuildChapterUrl(int chapterNumber)
    {
        return chapterUrlPattern.Replace("{1}", chapterNumber.ToString());
    }

    protected override Task<bool> CheckChapterExistsViaSeleniumRules(string url, int chapterNumber)
    {
        logger.LogInformation("Starting chapter existence check for {SiteName} using Selenium for URL: {Url}",
            GetSiteName(), url);

        var page = LoadChapterPage(url, chapterNumber);

        return Task.FromResult(page == ChapterPage.Released);
    }

    protected override Task<Chapter> ExtractNewChapterInfoRulesAsync(string url, int chapterNumber, int mangaId)
    {
        logger.LogInformation("Extracting chapter information from {SiteName} using Selenium for URL: {Url}",
            GetSiteName(), url);

        if (LoadChapterPage(url, chapterNumber) != ChapterPage.Released)
        {
            throw new ChapterCheckException($"Chapter {chapterNumber} is no longer available on {GetSiteName()} ({url})");
        }

        // e.g. "One Piece — Capítulo 1196: Não há valor no amor de quem quer morrer · Mugiwaras Oficial"
        var match = Regex.Match(webDriver.Title, @"Cap[ií]tulo\s+\d+\s*(?::\s*(?<title>[^·]*))?", RegexOptions.IgnoreCase);
        var title = match.Groups["title"].Value.Trim();

        var newChapter = new Chapter
        {
            MangaId = mangaId,
            Number = chapterNumber,
            Title = title.Length <= ChapterTitleMaxLength ? title : title[..ChapterTitleMaxLength],
            Url = url,
            ExtractedAt = DateTime.UtcNow
        };

        logger.LogInformation("Chapter information extracted: Chapter {ChapterNumber} - {ChapterTitle}",
            newChapter.Number, newChapter.Title);

        return Task.FromResult(newChapter);
    }

    /// <summary>
    /// Opens the chapter URL and waits until the page clearly is the chapter or the "not released" redirect
    /// </summary>
    /// <exception cref="ChapterCheckException">The page is neither (site changed, 404 page, never finished loading)</exception>
    private ChapterPage LoadChapterPage(string url, int chapterNumber)
    {
        webDriver.Navigate().GoToUrl(url);

        var chapterPath = $"{MangaPath}/{chapterNumber}";
        var chapterTitle = new Regex($@"Cap[ií]tulo\s+{chapterNumber}(?!\d)", RegexOptions.IgnoreCase);
        ChapterPage? result = null;

        try
        {
            new WebDriverWait(webDriver, PageTimeout).Until(driver =>
            {
                var path = new Uri(driver.Url).AbsolutePath.TrimEnd('/');

                if (path.Equals(MangaPath, StringComparison.OrdinalIgnoreCase))
                {
                    result = ChapterPage.NotReleased;
                    return true;
                }

                if (path.Equals(chapterPath, StringComparison.OrdinalIgnoreCase) && chapterTitle.IsMatch(driver.Title))
                {
                    result = ChapterPage.Released;
                    return true;
                }

                if (driver.FindElements(By.TagName("h1")).Any(h => h.Text.Contains("Página não encontrada", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new ChapterCheckException(
                        $"{GetSiteName()} answered 'page not found' for {url} (now at {driver.Url}). The chapter URL format probably changed.");
                }

                return false;
            });
        }
        catch (WebDriverTimeoutException ex)
        {
            throw new ChapterCheckException(
                $"{GetSiteName()} showed neither chapter {chapterNumber} nor the manga page within {PageTimeout.TotalSeconds}s " +
                $"(url: {webDriver.Url}, title: '{webDriver.Title}'). The site may have changed.", ex);
        }

        return result!.Value;
    }
}
