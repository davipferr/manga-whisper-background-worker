using Cronos;

namespace MangaWhisper.Application.Scheduling;

/// <summary>
/// Cron calculations for scraping jobs. The API uses the same rules (and the same Cronos library)
/// when a job is saved, so both projects agree on when a job runs next.
/// </summary>
public static class JobSchedule
{
    public static DateTime? GetNextOccurrence(string cronExpression, string timeZoneId, DateTime fromUtc)
    {
        var expression = CronExpression.Parse(cronExpression);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);

        return expression.GetNextOccurrence(fromUtc, timeZone);
    }
}
