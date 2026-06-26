using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.Repositories;

public interface IPartnerUserActionLimitRuleRepository
{
    Task EnsureIndexesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartnerUserActionLimitRule>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PartnerUserActionLimitRule?> GetByScopeAsync(string partnerId, string actorIdentifier, string action, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PartnerUserActionLimitRule>> GetByPartnerAsync(string partnerId, CancellationToken cancellationToken = default);

    Task UpsertAsync(PartnerUserActionLimitRule rule, CancellationToken cancellationToken = default);

    Task DeleteAsync(string partnerId, string actorIdentifier, string action, CancellationToken cancellationToken = default);
}
