using MangaWhisper.Domain.Entities;

namespace MangaWhisper.Domain.Interfaces;

public interface IChapterChecker
{
    /// <summary>
    /// Checks if a new chapter exists without extracting detailed information
    /// This is a lightweight operation that should be called first
    /// </summary>
    Task<bool> HasNewChapterAsync(MangaChecker checker);

    /// <summary>
    /// Extracts detailed chapter information including title, URL, and publication date
    /// This should only be called after HasNewChapterAsync returns true
    /// </summary>
    Task<Chapter?> ExtractNewChapterInfoAsync(MangaChecker checker);
}
