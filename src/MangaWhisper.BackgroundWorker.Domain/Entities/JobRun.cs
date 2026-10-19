using MangaWhisper.Common.Enums;

namespace MangaWhisper.Domain.Entities;

public class JobRun
{
    public const int ErrorMessageMaxLength = 1000;

    public int Id { get; set; }
    public int MangaCheckerId { get; set; }
    public int? ScrapingJobId { get; set; }
    public ScrapingJobType Type { get; set; }
    public JobRunStatus Status { get; set; }
    public DateTime RequestedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? FinishedAt { get; set; }
    public int ChaptersFound { get; set; }
    public string? ErrorMessage { get; set; }

    public virtual MangaChecker MangaChecker { get; set; } = null!;
    public virtual ScrapingJob? ScrapingJob { get; set; }

    public void Start()
    {
        Status = JobRunStatus.Running;
        StartedAt = DateTime.UtcNow;
    }

    public void Succeed(int chaptersFound)
    {
        Status = JobRunStatus.Succeeded;
        ChaptersFound = chaptersFound;
        FinishedAt = DateTime.UtcNow;
    }

    public void Fail(string errorMessage, int chaptersFound = 0)
    {
        Status = JobRunStatus.Failed;
        ChaptersFound = chaptersFound;
        ErrorMessage = Truncate(errorMessage);
        FinishedAt = DateTime.UtcNow;
    }

    public void Cancel(string reason)
    {
        Status = JobRunStatus.Cancelled;
        ErrorMessage = Truncate(reason);
        FinishedAt = DateTime.UtcNow;
    }

    private static string Truncate(string message)
    {
        return message.Length <= ErrorMessageMaxLength ? message : message[..ErrorMessageMaxLength];
    }
}
