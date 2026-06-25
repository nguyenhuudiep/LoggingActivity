using LoggingActivity.Web.Data;
using LoggingActivity.Web.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LoggingActivity.Web.Repositories;

public sealed class SystemAccessLogRepository : ISystemAccessLogRepository
{
    private readonly MongoDbContext _context;

    public SystemAccessLogRepository(MongoDbContext context)
    {
        _context = context;
    }

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<SystemAccessLog>(
                Builders<SystemAccessLog>.IndexKeys.Descending(log => log.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_system_access_logs_created_at_desc" }),
            new CreateIndexModel<SystemAccessLog>(
                Builders<SystemAccessLog>.IndexKeys.Ascending(log => log.UserName).Descending(log => log.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_system_access_logs_user_created_at" }),
            new CreateIndexModel<SystemAccessLog>(
                Builders<SystemAccessLog>.IndexKeys.Ascending(log => log.EventType).Descending(log => log.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_system_access_logs_event_created_at" })
        };

        return _context.SystemAccessLogs.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public Task AddAsync(SystemAccessLog logEntry, CancellationToken cancellationToken = default)
    {
        return _context.SystemAccessLogs.InsertOneAsync(logEntry, cancellationToken: cancellationToken);
    }

    public async Task<PagedResult<SystemAccessLog>> GetPagedAsync(SystemAccessLogQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);
        var page = Math.Max(1, query.Page);
        var pageSize = Math.Clamp(query.PageSize, 10, 100);

        var totalCount = await _context.SystemAccessLogs.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _context.SystemAccessLogs
            .Find(filter)
            .SortByDescending(log => log.CreatedAtUtc)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<SystemAccessLog>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    private static FilterDefinition<SystemAccessLog> BuildFilter(SystemAccessLogQuery query)
    {
        var builder = Builders<SystemAccessLog>.Filter;
        var filters = new List<FilterDefinition<SystemAccessLog>>();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            var regex = new BsonRegularExpression(term, "i");
            filters.Add(builder.Or(
                builder.Regex(log => log.UserName, regex),
                builder.Regex(log => log.DisplayName, regex),
                builder.Regex(log => log.Endpoint, regex),
                builder.Regex(log => log.IpAddress, regex),
                builder.Regex(log => log.Description, regex)));
        }

        if (!string.IsNullOrWhiteSpace(query.EventType))
        {
            filters.Add(builder.Eq(log => log.EventType, query.EventType.Trim()));
        }

        if (query.FromUtc.HasValue)
        {
            filters.Add(builder.Gte(log => log.CreatedAtUtc, query.FromUtc.Value));
        }

        if (query.ToUtc.HasValue)
        {
            filters.Add(builder.Lte(log => log.CreatedAtUtc, query.ToUtc.Value));
        }

        return filters.Count == 0 ? builder.Empty : builder.And(filters);
    }
}
