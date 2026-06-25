using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.Repositories;

public interface IUserActiveSessionRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);

    Task<UserActiveSession?> GetByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default);

    Task UpsertAsync(UserActiveSession session, CancellationToken cancellationToken = default);

    Task<bool> IsSessionActiveAsync(string normalizedUserName, string sessionId, CancellationToken cancellationToken = default);

    Task TouchAsync(string normalizedUserName, string sessionId, DateTime seenAtUtc, CancellationToken cancellationToken = default);

    Task RemoveIfMatchesAsync(string normalizedUserName, string sessionId, CancellationToken cancellationToken = default);
}
