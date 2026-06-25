using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.Repositories;

public interface ISystemAccessLogRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);

    Task AddAsync(SystemAccessLog logEntry, CancellationToken cancellationToken = default);

    Task<PagedResult<SystemAccessLog>> GetPagedAsync(SystemAccessLogQuery query, CancellationToken cancellationToken = default);
}
