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

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        // Mọi request của API partner tra partner theo ApiKey.
        var apiKeyIndex = new CreateIndexModel<Partner>(
            Builders<Partner>.IndexKeys.Ascending(partner => partner.ApiKey),
            new CreateIndexOptions { Name = "ix_partners_api_key" });

        return _context.Partners.Indexes.CreateOneAsync(apiKeyIndex, cancellationToken: cancellationToken);
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
        var safePageSize = Math.Clamp(query.PageSize, 1, 100);
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