using LoggingActivity.Web.Data;
using LoggingActivity.Web.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace LoggingActivity.Web.Repositories;

public sealed class PermissionGroupRepository : IPermissionGroupRepository
{
    private readonly MongoDbContext _context;

    public PermissionGroupRepository(MongoDbContext context)
    {
        _context = context;
    }

    public Task<PermissionGroup?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return _context.PermissionGroups.Find(group => group.Id == id).FirstOrDefaultAsync(cancellationToken)!;
    }

    public async Task<IReadOnlyList<PermissionGroup>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        var normalizedIds = ids
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList();

        if (normalizedIds.Count == 0)
        {
            return Array.Empty<PermissionGroup>();
        }

        return await _context.PermissionGroups.Find(group => normalizedIds.Contains(group.Id!))
            .SortBy(group => group.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PermissionGroup>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PermissionGroups.Find(group => group.IsActive)
            .SortBy(group => group.Name)
            .ToListAsync(cancellationToken);
    }

    public Task<PermissionGroup?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
    {
        var escaped = Regex.Escape(name.Trim());
        var regex = new BsonRegularExpression($"^{escaped}$", "i");
        return _context.PermissionGroups.Find(Builders<PermissionGroup>.Filter.Regex(group => group.Name, regex)).FirstOrDefaultAsync(cancellationToken)!;
    }

    public async Task<PagedResult<PermissionGroup>> GetPagedAsync(PermissionGroupQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 10 : query.PageSize;

        var totalCount = await _context.PermissionGroups.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _context.PermissionGroups.Find(filter)
            .SortBy(group => group.Name)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<PermissionGroup>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<PermissionGroupStatistics> GetStatisticsAsync(PermissionGroupQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);

        var totalGroups = await _context.PermissionGroups.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var activeGroups = await _context.PermissionGroups.CountDocumentsAsync(
            filter & Builders<PermissionGroup>.Filter.Eq(group => group.IsActive, true),
            cancellationToken: cancellationToken);
        var inactiveGroups = await _context.PermissionGroups.CountDocumentsAsync(
            filter & Builders<PermissionGroup>.Filter.Eq(group => group.IsActive, false),
            cancellationToken: cancellationToken);

        return new PermissionGroupStatistics
        {
            TotalGroups = totalGroups,
            ActiveGroups = activeGroups,
            InactiveGroups = inactiveGroups
        };
    }

    public Task CreateAsync(PermissionGroup group, CancellationToken cancellationToken = default)
    {
        return _context.PermissionGroups.InsertOneAsync(group, cancellationToken: cancellationToken);
    }

    public Task UpdateAsync(PermissionGroup group, CancellationToken cancellationToken = default)
    {
        group.UpdatedAtUtc = DateTime.UtcNow;
        return _context.PermissionGroups.ReplaceOneAsync(existing => existing.Id == group.Id, group, cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return _context.PermissionGroups.DeleteOneAsync(group => group.Id == id, cancellationToken);
    }

    private static FilterDefinition<PermissionGroup> BuildFilter(PermissionGroupQuery query)
    {
        var builder = Builders<PermissionGroup>.Filter;
        var filters = new List<FilterDefinition<PermissionGroup>>();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim();
            var regex = new BsonRegularExpression(searchTerm, "i");

            filters.Add(builder.Or(
                builder.Regex(group => group.Name, regex),
                builder.Regex(group => group.Description, regex)));
        }

        if (query.IsActive.HasValue)
        {
            filters.Add(builder.Eq(group => group.IsActive, query.IsActive.Value));
        }

        return filters.Count == 0 ? builder.Empty : builder.And(filters);
    }
}