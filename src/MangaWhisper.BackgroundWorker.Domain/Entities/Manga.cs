using MangaWhisper.Common.Enums;

namespace MangaWhisper.Domain.Entities;

public class Manga
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CoverImageUrl { get; set; } = string.Empty;
    public MangaStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public virtual ICollection<MangaChecker> MangaChecker { get; set; } = new List<MangaChecker>();
    public virtual ICollection<Chapter> Chapters { get; set; } = new List<Chapter>();

    public Chapter? GetLatestChapter()
    {
        return Chapters
            .OrderByDescending(c => c.Number)
            .FirstOrDefault();
    }
}
