using MangaWhisper.Domain.Entities;
using MangaWhisper.Domain.Exceptions;

namespace MangaWhisper.Domain.Interfaces;

public interface IChapterChecker
{
    /// <summary>
    /// Checks if the chapter after the last known one exists without extracting detailed information
    /// This is a lightweight operation that should be called first
    /// </summary>
    /// <returns>False only when the site clearly says the chapter is not released yet</returns>
    /// <exception cref="ChapterCheckException">The site could not be checked</exception>
    Task<bool> HasNewChapterAsync(MangaChecker checker);

    /// <summary>
    /// Extracts detailed chapter information including title and URL
    /// This should only be called after HasNewChapterAsync returns true
    /// </summary>
    /// <exception cref="ChapterCheckException">The chapter page could not be read</exception>
    Task<Chapter> ExtractNewChapterInfoAsync(MangaChecker checker);
}
