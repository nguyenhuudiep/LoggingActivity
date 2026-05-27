using LoggingActivity.Web.Data;
using LoggingActivity.Web.Models;
using MongoDB.Driver;
using MongoDB.Bson;

namespace LoggingActivity.Web.Repositories;

public sealed class LogActionDefinitionRepository : ILogActionDefinitionRepository
{
    private readonly MongoDbContext _context;

    public LogActionDefinitionRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<LogActionDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.LogActionDefinitions.Find(FilterDefinition<LogActionDefinition>.Empty)
            .SortBy(item => item.Code)
            .ToListAsync(cancellationToken);
    }

    public Task<LogActionDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return _context.LogActionDefinitions.Find(item => item.Code == code).FirstOrDefaultAsync(cancellationToken)!;
    }

    public async Task<IReadOnlyList<LogActionDefinition>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.LogActionDefinitions.Find(item => item.IsActive)
            .SortBy(item => item.Code)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<LogActionDefinition>> GetPagedAsync(LogActionQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 10 : query.PageSize;

        var totalCount = await _context.LogActionDefinitions.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _context.LogActionDefinitions.Find(filter)
            .SortBy(item => item.Code)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<LogActionDefinition>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task UpsertAsync(LogActionDefinition definition, string? existingCode = null, CancellationToken cancellationToken = default)
    {
        definition.UpdatedAtUtc = DateTime.UtcNow;

        var normalizedExistingCode = string.IsNullOrWhiteSpace(existingCode) ? null : existingCode.Trim();

        if (!string.IsNullOrWhiteSpace(normalizedExistingCode))
        {
            var existingDefinition = await _context.LogActionDefinitions
                .Find(item => item.Code == normalizedExistingCode)
                .FirstOrDefaultAsync(cancellationToken);

            if (existingDefinition is null)
            {
                return;
            }

            definition.Id = existingDefinition.Id;
            definition.CreatedAtUtc = existingDefinition.CreatedAtUtc;

            if (string.IsNullOrWhiteSpace(existingDefinition.Id))
            {
                await _context.LogActionDefinitions.ReplaceOneAsync(
                    item => item.Code == normalizedExistingCode,
                    definition,
                    new ReplaceOptions { IsUpsert = false },
                    cancellationToken);

                return;
            }

            await _context.LogActionDefinitions.ReplaceOneAsync(
                item => item.Id == existingDefinition.Id,
                definition,
                new ReplaceOptions { IsUpsert = false },
                cancellationToken);

            return;
        }

        if (string.IsNullOrWhiteSpace(definition.Id))
        {
            definition.Id = ObjectId.GenerateNewId().ToString();
            definition.CreatedAtUtc = DateTime.UtcNow;

            await _context.LogActionDefinitions.ReplaceOneAsync(
                item => item.Code == definition.Code,
                definition,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);

            return;
        }

        await _context.LogActionDefinitions.ReplaceOneAsync(
            item => item.Id == definition.Id,
            definition,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);
    }

    public Task DeleteAsync(string code, CancellationToken cancellationToken = default)
    {
        return _context.LogActionDefinitions.DeleteOneAsync(item => item.Code == code, cancellationToken);
    }

    private static FilterDefinition<LogActionDefinition> BuildFilter(LogActionQuery query)
    {
        var builder = Builders<LogActionDefinition>.Filter;
        var filters = new List<FilterDefinition<LogActionDefinition>>();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim();
            var regex = new BsonRegularExpression(searchTerm, "i");

            filters.Add(builder.Or(
                builder.Regex(item => item.Code, regex),
                builder.Regex(item => item.DisplayName, regex),
                builder.Regex(item => item.Description, regex)));
        }

        if (query.IsActive.HasValue)
        {
            filters.Add(builder.Eq(item => item.IsActive, query.IsActive.Value));
        }

        return filters.Count == 0 ? builder.Empty : builder.And(filters);
    }
}