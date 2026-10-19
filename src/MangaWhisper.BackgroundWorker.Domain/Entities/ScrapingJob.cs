using MangaWhisper.Common.Enums;

namespace MangaWhisper.Domain.Entities;

public class ScrapingJob
{
    public int Id { get; set; }
    public int MangaCheckerId { get; set; }
    public ScrapingJobType Type { get; set; }
    public string CronExpression { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public DateTime? NextRunAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public virtual MangaChecker MangaChecker { get; set; } = null!;
}
