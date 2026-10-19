using Microsoft.Extensions.Logging;
using MangaWhisper.Application.Scheduling;
using MangaWhisper.Common.Enums;
using MangaWhisper.Domain.Entities;
using MangaWhisper.Domain.Repositories;

namespace MangaWhisper.Application.Services;

public class JobRunnerService : IJobRunnerService
{
    private readonly IScrapingJobRepository _scrapingJobRepository;
    private readonly IJobRunRepository _jobRunRepository;
    private readonly IMangaCheckerRepository _mangaCheckerRepository;
    private readonly IChapterCheckingService _chapterCheckingService;
    private readonly ILogger<JobRunnerService> _logger;

    public JobRunnerService(
        IScrapingJobRepository scrapingJobRepository,
        IJobRunRepository jobRunRepository,
        IMangaCheckerRepository mangaCheckerRepository,
        IChapterCheckingService chapterCheckingService,
        ILogger<JobRunnerService> logger)
    {
        _scrapingJobRepository = scrapingJobRepository;
        _jobRunRepository = jobRunRepository;
        _mangaCheckerRepository = mangaCheckerRepository;
        _chapterCheckingService = chapterCheckingService;
        _logger = logger;
    }

    public async Task PrepareForStartupAsync(CancellationToken cancellationToken = default)
    {
        foreach (var run in await _jobRunRepository.GetByStatusAsync(JobRunStatus.Running, cancellationToken))
        {
            run.Fail("The worker stopped while this run was in progress.");
            await RestoreCheckerStatusAsync(run.MangaCheckerId);
            _logger.LogWarning("Run {RunId} was interrupted by a worker restart; marked as failed", run.Id);
        }

        foreach (var run in await _jobRunRepository.GetByStatusAsync(JobRunStatus.Pending, cancellationToken))
        {
            run.Cancel("Requested while the worker was not running. Request it again if it is still needed.");
            _logger.LogInformation("Run {RunId} was requested before the worker started; cancelled", run.Id);
        }

        var nowUtc = DateTime.UtcNow;

        foreach (var job in await _scrapingJobRepository.GetEnabledAsync(cancellationToken))
        {
            if (job.NextRunAt.HasValue && job.NextRunAt.Value > nowUtc)
            {
                continue;
            }

            var nextRunAt = JobSchedule.GetNextOccurrence(job.CronExpression, job.TimeZone, nowUtc);

            if (job.NextRunAt.HasValue)
            {
                _logger.LogInformation("Job {JobId} missed its run at {MissedRun:u} while the worker was down; next run at {NextRun:u}",
                    job.Id, job.NextRunAt.Value, nextRunAt);
            }

            job.NextRunAt = nextRunAt;
        }

        await _jobRunRepository.SaveChangesAsync(cancellationToken);
    }

    public async Task ScheduleDueJobsAsync(CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var dueJobs = await _scrapingJobRepository.GetDueAsync(nowUtc, cancellationToken);

        foreach (var job in dueJobs)
        {
            job.NextRunAt = JobSchedule.GetNextOccurrence(job.CronExpression, job.TimeZone, nowUtc);

            if (!job.MangaChecker.IsActive)
            {
                _logger.LogInformation("Job {JobId} skipped: checker {CheckerId} is paused. Next run at {NextRun:u}",
                    job.Id, job.MangaCheckerId, job.NextRunAt);
            }
            else if (await _jobRunRepository.HasOpenRunForCheckerAsync(job.MangaCheckerId, cancellationToken))
            {
                _logger.LogInformation("Job {JobId} skipped: checker {CheckerId} already has a run in progress. Next run at {NextRun:u}",
                    job.Id, job.MangaCheckerId, job.NextRunAt);
            }
            else
            {
                job.LastRunAt = nowUtc;

                await _jobRunRepository.AddAsync(new JobRun
                {
                    MangaCheckerId = job.MangaCheckerId,
                    ScrapingJobId = job.Id,
                    Type = job.Type,
                    Status = JobRunStatus.Pending,
                    RequestedAt = nowUtc
                }, cancellationToken);

                _logger.LogInformation("Job {JobId} ({Type}) queued for checker {CheckerId}. Next run at {NextRun:u}",
                    job.Id, job.Type, job.MangaCheckerId, job.NextRunAt);
            }

            // Saved per job so a second job of the same checker sees this run as open
            await _scrapingJobRepository.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExecuteNextPendingRunAsync(CancellationToken cancellationToken = default)
    {
        var run = await _jobRunRepository.GetNextPendingAsync(cancellationToken);

        if (run == null)
        {
            return false;
        }

        run.Start();
        await _jobRunRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Run {RunId} started: {Type} for checker {CheckerId}", run.Id, run.Type, run.MangaCheckerId);

        // Lets a failed run report the chapters it saved before failing (a backfill saves them one by one)
        var lastKnownChapterBefore = (await _mangaCheckerRepository.GetByIdAsNoTrackingAsync(run.MangaCheckerId))?.LastKnownChapter;

        try
        {
            await _chapterCheckingService.UpdateCheckerStatusAsync(run.MangaCheckerId, MangaCheckerStatus.Checking);

            var chaptersFound = run.Type switch
            {
                ScrapingJobType.CheckNext => await _chapterCheckingService.CheckNextChapterAsync(run.MangaCheckerId, cancellationToken),
                ScrapingJobType.Backfill => (await _chapterCheckingService.ProcessAllAvailableChaptersForCheckerAsync(run.MangaCheckerId, cancellationToken)).Count,
                _ => throw new InvalidOperationException($"Unknown job type {run.Type}")
            };

            // The checking service records scraping problems on the checker status instead of throwing
            var checker = await _mangaCheckerRepository.GetByIdAsNoTrackingAsync(run.MangaCheckerId);

            if (checker == null)
            {
                run.Fail("The checker no longer exists.", chaptersFound);
            }
            else if (checker.CheckerStatus == MangaCheckerStatus.Error)
            {
                run.Fail("The scraper reported an error. See the worker logs (docker compose logs worker).", chaptersFound);
            }
            else
            {
                await _chapterCheckingService.UpdateCheckerStatusAsync(run.MangaCheckerId,
                    checker.IsActive ? MangaCheckerStatus.Idle : MangaCheckerStatus.Stopped);
                run.Succeed(chaptersFound, DescribeResult(chaptersFound, checker));
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            run.Cancel("The worker stopped before the run finished.");
            await RestoreCheckerStatusAsync(run.MangaCheckerId);
            await _jobRunRepository.SaveChangesAsync(CancellationToken.None);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Run {RunId} failed", run.Id);
            run.Fail(ex.Message, await CountChaptersAdvancedAsync(run.MangaCheckerId, lastKnownChapterBefore));

            try
            {
                await _chapterCheckingService.UpdateCheckerStatusAsync(run.MangaCheckerId, MangaCheckerStatus.Error);
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update checker status to Error for checker {CheckerId}", run.MangaCheckerId);
            }
        }

        await _jobRunRepository.SaveChangesAsync(CancellationToken.None);

        _logger.LogInformation("Run {RunId} finished: {Status}, {ChaptersFound} chapter(s) saved",
            run.Id, run.Status, run.ChaptersFound);

        return true;
    }

    // Both job types save consecutive chapters and stop at the first one that is missing,
    // so the checker's last known chapter tells which chapters were saved and which one is next
    private static string DescribeResult(int chaptersFound, MangaChecker checker)
    {
        var notReleased = $"Chapter {checker.GetExpectedNextChapter()} is not released yet.";

        return chaptersFound switch
        {
            0 => notReleased,
            1 => $"Chapter {checker.LastKnownChapter} saved.",
            _ => $"Chapters {checker.LastKnownChapter - chaptersFound + 1} to {checker.LastKnownChapter} saved. {notReleased}"
        };
    }

    private async Task<int> CountChaptersAdvancedAsync(int checkerId, int? lastKnownChapterBefore)
    {
        try
        {
            var checker = await _mangaCheckerRepository.GetByIdAsNoTrackingAsync(checkerId);

            return checker != null && lastKnownChapterBefore.HasValue
                ? Math.Max(0, checker.LastKnownChapter - lastKnownChapterBefore.Value)
                : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to count the chapters saved by checker {CheckerId}", checkerId);
            return 0;
        }
    }

    private async Task RestoreCheckerStatusAsync(int checkerId)
    {
        try
        {
            var checker = await _mangaCheckerRepository.GetByIdAsNoTrackingAsync(checkerId);

            if (checker != null && checker.CheckerStatus == MangaCheckerStatus.Checking)
            {
                await _chapterCheckingService.UpdateCheckerStatusAsync(checkerId,
                    checker.IsActive ? MangaCheckerStatus.Idle : MangaCheckerStatus.Stopped);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore the status of checker {CheckerId}", checkerId);
        }
    }
}
