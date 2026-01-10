using Microsoft.Extensions.Logging;
using OpenQA.Selenium;
using OpenQA.Selenium.Support.UI;
using MangaWhisper.Domain.Entities;

namespace MangaWhisper.Domain.Services;

public class MugiwaraOficialChecker : BaseChapterChecker
{
    public override string SiteIdentifier => "mugiwara-oficial";
    protected override string chapterUrlPattern => "https://mugiwarasoficial.com/manga/manga-one-piece/capitulo-{1}/";
    protected override bool RequiresSelenium => true;

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
        try
        {
            return chapterUrlPattern.Replace("{1}", chapterNumber.ToString());
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error building chapter URL for chapter: {ChapterNumber}, Checker: {SiteName}",
                chapterNumber, GetSiteName());
            return string.Empty;
        }
    }

    protected override async Task<bool> CheckChapterExistsViaSeleniumRules(string url)
    {
        try
        {
            logger.LogInformation("Starting chapter existence check for {SiteName} using Selenium for URL: {Url}",
            GetSiteName(), url);

            webDriver.Navigate().GoToUrl(url);

            var wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(10));
            wait.Until(driver => driver.FindElement(By.TagName("body")));

            var mainColElement = webDriver.FindElement(By.CssSelector("div.content-area div.container div.row div.main-col"));
            var chapterHeadingElement = mainColElement.FindElement(By.CssSelector("h1#chapter-heading"));

            logger.LogInformation("Chapter existence check completed for {SiteName}. Chapter exists: {ChapterExists}",
                GetSiteName(), true);

            return await Task.FromResult(true);
        }
        catch (NoSuchElementException ex)
        {
            logger.LogInformation("Required elements not found on page for {SiteName}. Chapter does not exist. Details: '{ErrorDetails}'",
                GetSiteName(), ex.Message);
            return false;
        }
        catch (WebDriverTimeoutException ex)
        {
            logger.LogError(ex, "Timeout occurred while checking chapter existence for {SiteName}", GetSiteName());
            return false;
        }
        catch (WebDriverException ex)
        {
            logger.LogError(ex, "WebDriver error occurred while checking chapter existence for {SiteName}",
                GetSiteName());
            return false;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error checking if chapter exists from page source for {SiteName}",
                GetSiteName());
            return false;
        }
    }

    protected override async Task<Chapter?> ExtractNewChapterInfoRulesAsync(string url, int mangaId)
    {
        try
        {
            logger.LogInformation("Extracting chapter information for manga from {SiteName} using Selenium for URL: {Url}",
                GetSiteName(), url);

            webDriver.Navigate().GoToUrl(url);

            var wait = new WebDriverWait(webDriver, TimeSpan.FromSeconds(10));
            wait.Until(driver => driver.FindElement(By.TagName("body")));

            var mainElement = webDriver.FindElement(By.CssSelector("div.content-area div.container div.row div.main-col"));

            // This site does not provide a title, only the chapter number.
            // TODO: Create logic to fetch the manga title from another source if needed.
            var chapterHeadingElement = mainElement.FindElement(By.CssSelector("h1#chapter-heading"));
            var chapter = chapterHeadingElement.Text.Trim();

            var newChapter = new Chapter
            {
                MangaId = mangaId,
                Number = int.Parse(new string(chapter.Where(char.IsDigit).ToArray())),
                Title = "",
                Url = url,
                ExtractedAt = DateTime.UtcNow
            };

            logger.LogInformation("Chapter information extracted: ChapterTitle - {ChapterTitle}", newChapter.Title);
            return await Task.FromResult(newChapter);
        }
        catch (NoSuchElementException ex)
        {
            logger.LogWarning("Required elements not found on page for {SiteName}. Failed to extract chapter info. Details: '{ErrorDetails}'",
                GetSiteName(), ex.Message);
            return null;
        }
        catch (WebDriverTimeoutException ex)
        {
            logger.LogError(ex, "Timeout occurred while extracting chapter info for {SiteName}", GetSiteName());
            return null;
        }
        catch (WebDriverException ex)
        {
            logger.LogError(ex, "WebDriver error occurred while extracting chapter info for {SiteName}",
                GetSiteName());
            return null;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error extracting chapter info from page source for {SiteName}",
                GetSiteName());
            return null;
        }
    }
}
