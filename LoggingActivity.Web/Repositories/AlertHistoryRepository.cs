using LoggingActivity.Web.Data;
using LoggingActivity.Web.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace LoggingActivity.Web.Repositories;

public sealed class AlertHistoryRepository : IAlertHistoryRepository
{
    private readonly MongoDbContext _context;

    public AlertHistoryRepository(MongoDbContext context)
    {
        _context = context;
    }

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<AlertHistory>(
                Builders<AlertHistory>.IndexKeys
                    .Ascending(item => item.AlertDateUtc)
                    .Ascending(item => item.Action)
                    .Ascending(item => item.ActorIdentifier),
                new CreateIndexOptions { Name = "ix_alert_histories_date_action_actor" }),
            new CreateIndexModel<AlertHistory>(
                Builders<AlertHistory>.IndexKeys
                    .Ascending(item => item.AlertDateUtc)
                    .Ascending(item => item.Action)
                    .Ascending(item => item.UserId),
                new CreateIndexOptions { Name = "ix_alert_histories_date_action_user" }),
            new CreateIndexModel<AlertHistory>(
                Builders<AlertHistory>.IndexKeys.Descending(item => item.OccurredAtUtc),
                new CreateIndexOptions { Name = "ix_alert_histories_occurred_at_desc" })
        };

        return _context.AlertHistories.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public Task AddAsync(AlertHistory history, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(history.Id))
        {
            history.Id = MongoDB.Bson.ObjectId.GenerateNewId().ToString();
        }

        return _context.AlertHistories.InsertOneAsync(history, cancellationToken: cancellationToken);
    }

    public Task<bool> ExistsAsync(DateTime alertDateUtc, string actorIdentifier, string action, CancellationToken cancellationToken = default)
    {
        var normalizedActorIdentifier = ActorIdentityHelper.NormalizeIdentifier(actorIdentifier);
        var builder = Builders<AlertHistory>.Filter;
        var actorFilters = new List<FilterDefinition<AlertHistory>>
        {
            builder.Eq(item => item.ActorIdentifier, normalizedActorIdentifier)
        };

        if (ActorIdentityHelper.TryGetLegacyExternalUserId(normalizedActorIdentifier, out var legacyUserId))
        {
            actorFilters.Add(builder.Eq(item => item.UserId, legacyUserId));
        }

        var filter = builder.Eq(item => item.AlertDateUtc, alertDateUtc)
            & builder.Eq(item => item.Action, action)
            & builder.Or(actorFilters);

        return _context.AlertHistories.Find(filter).AnyAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<AlertHistory>> GetByAlertDateAsync(DateTime alertDateUtc, IReadOnlyCollection<string> actions, CancellationToken cancellationToken = default)
    {
        var builder = Builders<AlertHistory>.Filter;
        var filter = builder.Eq(item => item.AlertDateUtc, alertDateUtc)
            & builder.In(item => item.Action, actions);

        return await _context.AlertHistories.Find(filter)
            .Project<AlertHistory>(Builders<AlertHistory>.Projection
                .Include(item => item.ActorIdentifier)
                .Include(item => item.UserId)
                .Include(item => item.Action))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<AlertHistory>> GetPagedAsync(AlertHistoryQuery query, CancellationToken cancellationToken = default)
    {
        var builder = Builders<AlertHistory>.Filter;
        var filters = new List<FilterDefinition<AlertHistory>>();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            var regex = new BsonRegularExpression(System.Text.RegularExpressions.Regex.Escape(term), "i");
            if (int.TryParse(term, out var userId))
            {
                filters.Add(builder.Or(
                    builder.Regex(item => item.UserName, regex),
                    builder.Regex(item => item.ActorIdentifier, regex),
                    builder.Eq(item => item.UserId, userId)));
            }
            else
            {
                filters.Add(builder.Or(
                    builder.Regex(item => item.UserName, regex),
                    builder.Regex(item => item.ActorIdentifier, regex)));
            }
        }

        if (!string.IsNullOrWhiteSpace(query.PartnerId))
        {
            filters.Add(builder.Eq(item => item.PartnerId, query.PartnerId));
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            filters.Add(builder.Regex(item => item.Action, new BsonRegularExpression($"^{Regex.Escape(query.Action.Trim())}$", "i")));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            if (string.Equals(query.Status, "at-limit", StringComparison.OrdinalIgnoreCase))
            {
                filters.Add(new BsonDocument("$expr", new BsonDocument("$eq", new BsonArray { "$CurrentCount", "$DailyLimit" })));
            }
            else if (string.Equals(query.Status, "exceeded", StringComparison.OrdinalIgnoreCase))
            {
                filters.Add(new BsonDocument("$expr", new BsonDocument("$gt", new BsonArray { "$CurrentCount", "$DailyLimit" })));
            }
        }

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