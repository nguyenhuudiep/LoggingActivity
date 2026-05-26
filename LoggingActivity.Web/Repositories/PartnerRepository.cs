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