using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.Repositories;

public interface IAlertRuleRepository
{
    Task<AlertRule?> GetByActionAsync(string action, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AlertRule>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<AlertRule>> GetPagedAsync(AlertRuleQuery query, CancellationToken cancellationToken = default);

    Task UpsertAsync(AlertRule rule, CancellationToken cancellationToken = default);

    Task DeleteAsync(string action, CancellationToken cancellationToken = default);
}