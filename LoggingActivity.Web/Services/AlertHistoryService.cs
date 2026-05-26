using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;

namespace LoggingActivity.Web.Services;

public sealed class AlertHistoryService
{
    private readonly IAlertHistoryRepository _alertHistoryRepository;

    public AlertHistoryService(IAlertHistoryRepository alertHistoryRepository)
    {
        _alertHistoryRepository = alertHistoryRepository;
    }

    public Task AddAsync(AlertHistory history, CancellationToken cancellationToken = default)
    {
        return _alertHistoryRepository.AddAsync(history, cancellationToken);
    }

    public Task<bool> ExistsAsync(DateTime alertDateUtc, int userId, string action, CancellationToken cancellationToken = default)
    {
        return _alertHistoryRepository.ExistsAsync(alertDateUtc, userId, action, cancellationToken);
    }

    public Task<PagedResult<AlertHistory>> GetPagedAsync(AlertHistoryQuery query, CancellationToken cancellationToken = default)
    {
        return _alertHistoryRepository.GetPagedAsync(query, cancellationToken);
    }
}