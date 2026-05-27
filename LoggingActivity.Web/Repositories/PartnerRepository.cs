using LoggingActivity.Web.Data;
using LoggingActivity.Web.Models;
using MongoDB.Driver;

namespace LoggingActivity.Web.Repositories;

public sealed class PartnerRepository : IPartnerRepository
{
    private readonly MongoDbContext _context;

    public PartnerRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Partner>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Partners.Find(FilterDefinition<Partner>.Empty)
            .SortBy(partner => partner.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<Partner>> GetPagedAsync(PartnerQuery query, CancellationToken cancellationToken = default)
    {
        var safePage = Math.Max(1, query.Page);
        var safePageSize = Math.Max(1, query.PageSize);
        var filter = FilterDefinition<Partner>.Empty;

        var totalCount = await _context.Partners.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _context.Partners.Find(filter)
            .SortBy(partner => partner.Name)
            .Skip((safePage - 1) * safePageSize)
            .Limit(safePageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Partner>
        {
            Items = items,
            TotalCount = totalCount,
            Page = safePage,
            PageSize = safePageSize
        };
    }

    public Task<Partner?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return _context.Partners.Find(partner => partner.Id == id).FirstOrDefaultAsync(cancellationToken)!;
    }

    public Task<Partner?> GetByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        return _context.Partners.Find(partner => partner.ApiKey == apiKey && partner.IsActive).FirstOrDefaultAsync(cancellationToken)!;
    }

    public Task CreateAsync(Partner partner, CancellationToken cancellationToken = default)
    {
        return _context.Partners.InsertOneAsync(partner, cancellationToken: cancellationToken);
    }

    public Task UpdateAsync(Partner partner, CancellationToken cancellationToken = default)
    {
        partner.UpdatedAtUtc = DateTime.UtcNow;
        return _context.Partners.ReplaceOneAsync(existing => existing.Id == partner.Id, partner, cancellationToken: cancellationToken);
    }
}