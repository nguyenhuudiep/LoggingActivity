using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.Repositories;

public interface IActivityLogIngestQueueRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);

    Task<bool> EnqueueAsync(ActivityLogIngestQueueItem item, CancellationToken cancellationToken = default);

    Task<long> DeleteDemoAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<ActivityLogIngestQueueItem>> GetPagedAsync(ActivityLogIngestQueueQuery query, CancellationToken cancellationToken = default);

    Task<ActivityLogIngestQueueSummary> GetSummaryAsync(ActivityLogIngestQueueQuery query, CancellationToken cancellationToken = default);

    Task<ActivityLogIngestQueueItem?> TryLeaseNextAsync(string workerId, DateTime nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken = default);

    Task MarkCompletedAsync(string id, CancellationToken cancellationToken = default);

    Task MarkPendingAsync(string id, string error, TimeSpan retryDelay, CancellationToken cancellationToken = default);

    Task MarkFailedAsync(string id, string error, CancellationToken cancellationToken = default);

    Task<bool> RetryFailedAsync(string id, CancellationToken cancellationToken = default);

    Task<long> RetryFailedAsync(ActivityLogIngestQueueQuery query, CancellationToken cancellationToken = default);
}