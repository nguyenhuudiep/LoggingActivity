using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.Repositories;

public interface IAlertHistoryRepository
{
    Task AddAsync(AlertHistory history, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(DateTime alertDateUtc, string actorIdentifier, string action, CancellationToken cancellationToken = default);

    Task<PagedResult<AlertHistory>> GetPagedAsync(AlertHistoryQuery query, CancellationToken cancellationToken = default);
}