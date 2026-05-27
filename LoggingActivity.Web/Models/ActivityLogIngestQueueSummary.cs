namespace LoggingActivity.Web.Models;

public sealed class ActivityLogIngestQueueSummary
{
    public long TotalCount { get; init; }

    public long PendingCount { get; init; }

    public long ProcessingCount { get; init; }

    public long FailedCount { get; init; }

    public long CompletedCount { get; init; }
}