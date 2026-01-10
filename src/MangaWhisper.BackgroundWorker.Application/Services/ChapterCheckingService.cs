using Microsoft.Extensions.Logging;
using MangaWhisper.Domain.Entities;
using MangaWhisper.Domain.Factories;
using MangaWhisper.Domain.Repositories;
using MangaWhisper.Common.Enums;

namespace MangaWhisper.Application.Services;

public class ChapterCheckingService : IChapterCheckingService
{
    private readonly IChapterCheckerFactory _checkerFactory;
    private readonly ILogger<ChapterCheckingService> _logger;
    private readonly IMangaCheckerRepository _mangaCheckerRepository;
    private readonly IChapterRepository _chapterRepository;
    private readonly IMangaRepository _mangaRepository;

    public ChapterCheckingService(
        IChapterCheckerFactory checkerFactory,
        ILogger<ChapterCheckingService> logger,
        IMangaCheckerRepository mangaCheckerRepository,
        IChapterRepository chapterRepository,
        IMangaRepository mangaRepository)
    {
        _checkerFactory = checkerFactory;
        _logger = logger;
        _mangaCheckerRepository = mangaCheckerRepository;
        _chapterRepository = chapterRepository;
        _mangaRepository = mangaRepository;
    }

    public async Task<IEnumerable<MangaChecker>> GetActiveCheckersAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving active manga checkers");
            return await _mangaCheckerRepository.GetActiveCheckersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving active manga checkers");
            throw;
        }
    }

    public async Task<IEnumerable<MangaChecker>> GetAllCheckersAsync()
    {
        try
        {
            _logger.LogInformation("Retrieving all manga checkers");
            return await _mangaCheckerRepository.GetAllCheckersAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving all manga checkers");
            throw;
        }
    }

    public async Task AddCheckerAsync(MangaChecker checker)
    {
        try
        {
            _logger.LogInformation("Adding new manga checker for manga: {MangaTitle}", checker.Manga.Title);

            if (checker == null)
                throw new ArgumentNullException(nameof(checker));

            if (string.IsNullOrWhiteSpace(checker.Manga.Title))
                throw new ArgumentException("Manga title cannot be empty", nameof(checker));

            var existingManga = await _mangaRepository.GetByTitleAsync(checker.Manga.Title);

            if (existingManga != null)
            {
                checker.MangaId = existingManga.Id;
                checker.Manga = existingManga;
            }
            else
            {
                await _mangaRepository.AddAsync(checker.Manga);
                await _mangaRepository.SaveChangesAsync();
            }

            await _mangaCheckerRepository.AddAsync(checker);
            await _mangaCheckerRepository.SaveChangesAsync();

            _logger.LogInformation("Successfully added manga checker with ID: {CheckerId}", checker.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding manga checker for manga: {MangaTitle}", checker?.Manga?.Title);
            throw;
        }
    }

    public async Task<bool> HasNewChapterAsync(MangaChecker checker)
    {
        try
        {
            var chapterChecker = _checkerFactory.CreateChecker(checker.SiteIdentifier);
            return await chapterChecker.HasNewChapterAsync(checker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if new chapter exists for checker {CheckerId}", checker.Id);
            return false;
        }
    }

    public async Task<Chapter?> ExtractNewChapterInfoAsync(MangaChecker checker)
    {
        try
        {
            var chapterChecker = _checkerFactory.CreateChecker(checker.SiteIdentifier);
            return await chapterChecker.ExtractNewChapterInfoAsync(checker);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting new chapter info for checker {CheckerId}", checker.Id);
            return null;
        }
    }

    public async Task UpdateCheckerStatusAsync(int checkerId, MangaCheckerStatus status)
    {
        try
        {
            await _mangaCheckerRepository.UpdateStatusAsync(checkerId, status);
            await _mangaCheckerRepository.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating checker status for checker {CheckerId}", checkerId);
            throw;
        }
    }

    public async Task SaveNewChapterAsync(Chapter newChapter, MangaChecker checker)
    {
        try
        {
            var existingChapter = await _chapterRepository.GetByMangaAndNumberAsync(newChapter.MangaId, newChapter.Number);

            if (existingChapter != null)
            {
                _logger.LogInformation("Chapter {ChapterNumber} for manga {MangaId} already exists. Skipping save.",
                    newChapter.Number, newChapter.MangaId);
                return;
            }

            await _chapterRepository.AddAsync(newChapter);
            await _chapterRepository.SaveChangesAsync();

            checker.LastKnownChapter = newChapter.Number;
            await _mangaCheckerRepository.UpdateStatusAsync(checker.Id, checker.CheckerStatus);

            await _mangaCheckerRepository.SaveChangesAsync();

            _logger.LogInformation("Saved new chapter {ChapterNumber} for manga {MangaId}",
                newChapter.Number, newChapter.MangaId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving new chapter {ChapterNumber} for manga {MangaId}",
                newChapter.Number, newChapter.MangaId);
            throw;
        }
    }

    public async Task<List<Chapter>> ProcessAllAvailableChaptersForCheckerAsync(int checkerId, CancellationToken cancellationToken = default)
    {
        var foundChapters = new List<Chapter>();

        try
        {
            _logger.LogInformation("Starting to process all available chapters for checker {CheckerId}", checkerId);

            var checker = await _mangaCheckerRepository.GetByIdAsync(checkerId);

            if (checker == null)
            {
                _logger.LogWarning("Checker with ID {CheckerId} not found", checkerId);
                throw new ArgumentException($"Checker with ID {checkerId} not found");
            }

            bool hasMoreChapters = true;
            int processedCount = 0;

            while (hasMoreChapters && !cancellationToken.IsCancellationRequested)
            {
                try
                {
                    checker = await _mangaCheckerRepository.GetByIdAsync(checkerId);

                    if (checker == null)
                    {
                        _logger.LogWarning("Checker {CheckerId} no longer exists", checkerId);
                        break;
                    }

                    var hasNewChapter = await HasNewChapterAsync(checker);

                    if (!hasNewChapter)
                    {
                        _logger.LogInformation("No more chapters available for manga {MangaTitle}. Total processed: {Count}",
                            checker.Manga?.Title ?? "Unknown", processedCount);

                        hasMoreChapters = false;
                        break;
                    }

                    var newChapter = await ProcessCheckerAsync(checker, cancellationToken);

                    if (newChapter != null)
                    {
                        foundChapters.Add(newChapter);
                        processedCount++;

                        _logger.LogInformation("Successfully processed chapter {ChapterNumber} for manga {MangaTitle}. Total: {Count}",
                            newChapter.Number, checker.Manga?.Title ?? "Unknown", processedCount);
                    }
                    else
                    {
                        _logger.LogWarning("No new chapter found for manga {MangaTitle} from site {SiteIdentifier}. Stopping batch processing.",
                            checker.Manga?.Title ?? "Unknown", checker.SiteIdentifier);

                        hasMoreChapters = false;
                    }
                }
                catch (OperationCanceledException)
                {
                    _logger.LogInformation("Processing cancelled for checker {CheckerId} after {Count} chapters", checkerId, processedCount);
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error processing chapter for checker {CheckerId}. Stopping batch processing.", checkerId);
                    hasMoreChapters = false;
                    
                    try
                    {
                        await UpdateCheckerStatusAsync(checkerId, MangaCheckerStatus.Error);
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Failed to update checker status to Error for checker {CheckerId}", checkerId);
                    }
                }
            }

            _logger.LogInformation("Completed processing all available chapters for checker {CheckerId}. Total chapters found: {Count}",
                checkerId, foundChapters.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during batch processing for checker {CheckerId}", checkerId);
            throw;
        }

        return foundChapters;
    }

    public async Task CheckAllActiveCheckersManuallyAsync(CancellationToken cancellationToken = default)
    {
        await CheckAllActiveCheckersManuallyAsync(cancellationToken, returnChapters: false);
    }

    public async Task<List<Chapter>> CheckAllActiveCheckersManuallyAsync(CancellationToken cancellationToken, bool returnChapters)
    {
        var foundChapters = new List<Chapter>();

        try
        {
            _logger.LogInformation("Manual check triggered for all active checkers");

            var activeCheckers = await GetActiveCheckersAsync();

            foreach (var checker in activeCheckers)
            {
                if (cancellationToken.IsCancellationRequested)
                    break;

                try
                {
                    var newChapter = await ProcessCheckerAsync(checker, cancellationToken);
                    if (returnChapters && newChapter != null)
                    {
                        foundChapters.Add(newChapter);
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error checking manga {MangaTitle} with checker {CheckerId}",
                        checker.Manga?.Title ?? "Unknown", checker.Id);

                    try
                    {
                        await UpdateCheckerStatusAsync(checker.Id, MangaCheckerStatus.Error);
                    }
                    catch (Exception updateEx)
                    {
                        _logger.LogError(updateEx, "Failed to update checker status to Error for checker {CheckerId}", checker.Id);
                    }
                }
            }

            _logger.LogInformation("Manual check completed for all active checkers");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during manual check of all active checkers");
            throw;
        }

        return foundChapters;
    }

    private async Task<Chapter?> ProcessCheckerAsync(MangaChecker checker, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Checking manga {MangaTitle} with site {SiteIdentifier}",
            checker.Manga?.Title ?? "Unknown", checker.SiteIdentifier);

        await UpdateCheckerStatusAsync(checker.Id, MangaCheckerStatus.Checking);

        var hasNewChapter = await HasNewChapterAsync(checker);

        if (!hasNewChapter)
        {
            _logger.LogInformation("No new chapter found for manga {MangaTitle} from site {SiteIdentifier}",
                checker.Manga?.Title ?? "Unknown", checker.SiteIdentifier);
            await UpdateCheckerStatusAsync(checker.Id, MangaCheckerStatus.Idle);
            return null;
        }

        _logger.LogInformation(
            "New chapter found for manga {MangaTitle}: Chapter {ChapterNumber} from site {SiteIdentifier}",
            checker.Manga?.Title ?? "Unknown",
            checker.GetExpectedNextChapter(),
            checker.SiteIdentifier);

        var newChapter = await ExtractNewChapterInfoAsync(checker);

        if (newChapter == null)
        {
            _logger.LogWarning("New chapter was detected but failed to extract chapter information for manga {MangaTitle} from site {SiteIdentifier}",
                checker.Manga?.Title ?? "Unknown", checker.SiteIdentifier);
            await UpdateCheckerStatusAsync(checker.Id, MangaCheckerStatus.Error);
            return null;
        }

        await SaveNewChapterAsync(newChapter, checker);
        await UpdateCheckerStatusAsync(checker.Id, MangaCheckerStatus.Idle);

        return newChapter;
    }
}
