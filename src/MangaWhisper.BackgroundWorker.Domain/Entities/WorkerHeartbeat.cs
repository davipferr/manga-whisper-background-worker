namespace MangaWhisper.Domain.Entities;

public class WorkerHeartbeat
{
    // The table has a single row
    public const int SingletonId = 1;

    public int Id { get; set; }
    public DateTime LastSeenAt { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? StoppedAt { get; set; }
}
