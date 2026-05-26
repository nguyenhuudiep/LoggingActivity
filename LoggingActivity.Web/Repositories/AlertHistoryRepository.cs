using LoggingActivity.Web.Data;
using LoggingActivity.Web.Models;
using MongoDB.Driver;

namespace LoggingActivity.Web.Repositories;

public sealed class AlertHistoryRepository : IAlertHistoryRepository
{
    private readonly MongoDbContext _context;

    public AlertHistoryRepository(MongoDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(AlertHistory history, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(history.Id))
        {
            history.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        }

        return _context.AlertHistories.InsertOneAsync(history, cancellationToken: cancellationToken);
    }

    public Task<bool> ExistsAsync(DateTime alertDateUtc, int userId, string action, CancellationToken cancellationToken = default)
    {
        var filter = Builders<AlertHistory>.Filter.Eq(item => item.AlertDateUtc, alertDateUtc)
            & Builders<AlertHistory>.Filter.Eq(item => item.UserId, userId)
            & Builders<AlertHistory>.Filter.Eq(item => item.Action, action);

        return _context.AlertHistories.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<PagedResult<AlertHistory>> GetPagedAsync(AlertHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var builder = Builders<AlertHistory>.Filter;
        var filters = new List<FilterDefinition<AlertHistory>>();

        if (query.FromUtc.HasValue)
        {
            filters.Add(builder.Gte(item => item.OccurredAtUtc, query.FromUtc.Value));
        }

        if (query.ToUtc.HasValue)
        {
            filters.Add(builder.Lte(item => item.OccurredAtUtc, query.ToUtc.Value));
        }

        var filter = filters.Count == 0 ? builder.Empty : builder.And(filters);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 5, 100);
        var totalCount = await _context.AlertHistories.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _context.AlertHistories.Find(filter)
            .SortByDescending(item => item.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AlertHistory>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }
}