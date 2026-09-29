using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.Repositories;

public interface IAlertHistoryRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);

    Task AddAsync(AlertHistory history, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(DateTime alertDateUtc, string actorIdentifier, string action, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertHistory>> GetByAlertDateAsync(DateTime alertDateUtc, IReadOnlyCollection<string> actions, CancellationToken cancellationToken = default);

    Task<PagedResult<AlertHistory>> GetPagedAsync(AlertHistoryQuery query, CancellationToken cancellationToken = default);
}