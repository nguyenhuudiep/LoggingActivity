using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.Repositories;

public interface IActivityLogRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);

    Task AddAsync(ActivityLog logEntry, CancellationToken cancellationToken = default);

    Task<PagedResult<ActivityLog>> GetPagedAsync(LogQuery query, CancellationToken cancellationToken = default);

    Task<PagedResult<ActivityLog>> GetPagedByUserAsync(string userId, LogQuery query, CancellationToken cancellationToken = default);

    Task<LogStatistics> GetStatisticsAsync(LogQuery query, CancellationToken cancellationToken = default);

    Task<LogStatistics> GetStatisticsByUserAsync(string userId, LogQuery query, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, long>> GetActionCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertWarning>> GetUserActionCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DailyAlertCount>> GetDailyUserActionCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    Task<long> GetUserActionCountAsync(string actorIdentifier, string? actorIdentifierType, string action, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    Task<long> GetPartnerUserActionCountAsync(string partnerId, string actorIdentifier, string? actorIdentifierType, string action, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<string, long>> GetActionCountsAsync(CancellationToken cancellationToken = default);
}