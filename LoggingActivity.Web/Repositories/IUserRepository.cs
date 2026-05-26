using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.Repositories;

public interface IUserRepository
{
    Task<AppUser?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<AppUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<AppUser>> GetPagedAsync(UserQuery query, CancellationToken cancellationToken = default);

    Task<UserStatistics> GetStatisticsAsync(UserQuery query, CancellationToken cancellationToken = default);

    Task CreateAsync(AppUser user, CancellationToken cancellationToken = default);

    Task UpdateAsync(AppUser user, CancellationToken cancellationToken = default);
}