namespace MangaWhisper.Domain.Exceptions;

/// <summary>
/// The site could not be checked: the page didn't load, the browser failed, or the page didn't look like
/// either a chapter or a "not released yet" answer (usually because the site changed).
/// Distinct from "the chapter is not released yet", which checkers return as false.
/// </summary>
public class ChapterCheckException : Exception
{
    public ChapterCheckException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}
