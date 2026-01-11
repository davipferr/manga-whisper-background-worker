using MangaWhisper.Common.Enums;
using MangaWhisper.Domain.Entities;

namespace MangaWhisper.Application.Services;

public interface IChapterCheckingService
{
    Task<IEnumerable<MangaChecker>> GetActiveCheckersAsync();
    Task UpdateCheckerStatusAsync(int checkerId, MangaCheckerStatus status);
    Task AddCheckerAsync(MangaChecker checker);
    Task SaveNewChapterAsync(Chapter newChapter, MangaChecker checker);

    /// <summary>
    /// Checks if a new chapter exists for the given manga checker
    /// </summary>
    /// <param name="checker">The manga checker to verify</param>
    /// <returns>True if a new chapter exists, false otherwise</returns>
    Task<bool> HasNewChapterAsync(MangaChecker checker);

    /// <summary>
    /// Extracts detailed information about the new chapter
    /// This method should only be called after HasNewChapterAsync returns true
    /// </summary>
    /// <param name="checker">The manga checker to extract chapter info from</param>
    /// <returns>The new chapter information or null if extraction fails</returns>
    Task<Chapter?> ExtractNewChapterInfoAsync(MangaChecker checker);

    /// <summary>
    /// Processes all available chapters for a specific manga checker
    /// </summary>
    /// <param name="checkerId">The ID of the manga checker to process</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>A list of all chapters that were found and processed</returns>
    Task<List<Chapter>> ProcessAllAvailableChaptersForCheckerAsync(int checkerId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Manually triggers a check for all active checkers
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    Task CheckAllActiveCheckersAsync(CancellationToken cancellationToken = default);
}
