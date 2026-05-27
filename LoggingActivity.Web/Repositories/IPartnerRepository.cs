using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.Repositories;

public interface IPartnerRepository
{
    Task<IReadOnlyList<Partner>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<Partner>> GetPagedAsync(PartnerQuery query, CancellationToken cancellationToken = default);

    Task<Partner?> GetByIdAsync(string id, CancellationToken cancellationToken = default);

    Task<Partner?> GetByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default);

    Task CreateAsync(Partner partner, CancellationToken cancellationToken = default);

    Task UpdateAsync(Partner partner, CancellationToken cancellationToken = default);
}