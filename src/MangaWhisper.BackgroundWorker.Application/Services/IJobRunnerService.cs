namespace MangaWhisper.Application.Services;

public interface IJobRunnerService
{
    /// <summary>
    /// Runs once when the worker starts. Nothing is executed here: runs left Pending/Running by a
    /// previous process are closed, and jobs whose next run is already in the past are moved to
    /// their next future occurrence (missed runs are skipped, not caught up).
    /// </summary>
    Task PrepareForStartupAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a Pending run for every enabled job that is due and moves the job to its next occurrence
    /// </summary>
    Task ScheduleDueJobsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes the oldest Pending run
    /// </summary>
    /// <returns>False when there was nothing to run</returns>
    Task<bool> ExecuteNextPendingRunAsync(CancellationToken cancellationToken = default);
}
