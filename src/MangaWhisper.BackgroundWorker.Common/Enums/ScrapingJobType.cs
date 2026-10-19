namespace MangaWhisper.Common.Enums;

public enum ScrapingJobType
{
    // Looks once for chapter LastKnownChapter + 1
    CheckNext = 1,
    // Walks every chapter from LastKnownChapter + 1 until one is missing
    Backfill = 2,
}
