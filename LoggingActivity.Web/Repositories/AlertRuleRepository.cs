using LoggingActivity.Web.Data;
using LoggingActivity.Web.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LoggingActivity.Web.Repositories;

public sealed class AlertRuleRepository : IAlertRuleRepository
{
    private readonly MongoDbContext _context;

    public AlertRuleRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AlertRules.Find(FilterDefinition<AlertRule>.Empty)
            .SortBy(rule => rule.Action)
            .ToListAsync(cancellationToken);
    }

    public Task<AlertRule?> GetByActionAsync(string action, CancellationToken cancellationToken = default)
    {
        return _context.AlertRules.Find(rule => rule.Action == action).FirstOrDefaultAsync(cancellationToken)!;
    }

    public async Task<IReadOnlyList<AlertRule>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return await _context.AlertRules.Find(rule => rule.IsActive)
            .SortBy(rule => rule.Action)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<AlertRule>> GetPagedAsync(AlertRuleQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 10 : query.PageSize;

        var totalCount = await _context.AlertRules.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _context.AlertRules.Find(filter)
            .SortBy(rule => rule.Action)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AlertRule>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public Task UpsertAsync(AlertRule rule, CancellationToken cancellationToken = default)
    {
        rule.UpdatedAtUtc = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(rule.Id))
        {
            rule.Id = ObjectId.GenerateNewId().ToString();
            rule.CreatedAtUtc = DateTime.UtcNow;

            return _context.AlertRules.ReplaceOneAsync(
                existing => existing.Action == rule.Action,
                rule,
                new ReplaceOptions { IsUpsert = true },
                cancellationToken);
        }

        return _context.AlertRules.ReplaceOneAsync(
            existing => existing.Id == rule.Id,
            rule,
            new ReplaceOptions { IsUpsert = false },
            cancellationToken);
    }

    public Task DeleteAsync(string action, CancellationToken cancellationToken = default)
    {
        return _context.AlertRules.DeleteOneAsync(rule => rule.Action == action, cancellationToken);
    }

    private static FilterDefinition<AlertRule> BuildFilter(AlertRuleQuery query)
    {
        var builder = Builders<AlertRule>.Filter;
        var filters = new List<FilterDefinition<AlertRule>>();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var regex = new BsonRegularExpression(query.SearchTerm.Trim(), "i");
            filters.Add(builder.Regex(rule => rule.Action, regex));
        }

        if (!string.IsNullOrWhiteSpace(query.ActionCode))
        {
            filters.Add(builder.Eq(rule => rule.Action, query.ActionCode.Trim()));
        }

        if (query.IsActive.HasValue)
        {
            filters.Add(builder.Eq(rule => rule.IsActive, query.IsActive.Value));
        }

        return filters.Count == 0 ? builder.Empty : builder.And(filters);
    }
}