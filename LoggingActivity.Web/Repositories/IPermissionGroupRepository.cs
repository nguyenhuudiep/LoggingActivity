using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.Repositories;

public interface IPermissionGroupRepository
{
    Task<PermissionGroup?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionGroup>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PermissionGroup>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<PermissionGroup?> GetByNameAsync(string name, CancellationToken cancellationToken = default);

    Task<PagedResult<PermissionGroup>> GetPagedAsync(PermissionGroupQuery query, CancellationToken cancellationToken = default);

    Task<PermissionGroupStatistics> GetStatisticsAsync(PermissionGroupQuery query, CancellationToken cancellationToken = default);

    Task CreateAsync(PermissionGroup group, CancellationToken cancellationToken = default);

    Task UpdateAsync(PermissionGroup group, CancellationToken cancellationToken = default);

    Task DeleteAsync(string id, CancellationToken cancellationToken = default);
}