using LoggingActivity.Web.Data;
using LoggingActivity.Web.Infrastructure;
using LoggingActivity.Web.Models;
using MongoDB.Bson;
using MongoDB.Driver;

namespace LoggingActivity.Web.Repositories;

public sealed class ActivityLogRepository : IActivityLogRepository
{
    private readonly MongoDbContext _context;

    public ActivityLogRepository(MongoDbContext context)
    {
        _context = context;
    }

    public Task AddAsync(ActivityLog logEntry, CancellationToken cancellationToken = default)
    {
        return _context.ActivityLogs.InsertOneAsync(logEntry, cancellationToken: cancellationToken);
    }

    public async Task<PagedResult<ActivityLog>> GetPagedAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);
        return await GetPagedInternalAsync(filter, query, cancellationToken);
    }

    public async Task<PagedResult<ActivityLog>> GetPagedByUserAsync(string userId, LogQuery query, CancellationToken cancellationToken = default)
    {
        var scopedQuery = new LogQuery
        {
            SearchTerm = query.SearchTerm,
            PartnerId = userId,
            Action = query.Action,
            Source = query.Source,
            ActorIdentifier = query.ActorIdentifier,
            ActorIdentifierType = query.ActorIdentifierType,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc,
            Page = query.Page,
            PageSize = query.PageSize
        };

        return await GetPagedInternalAsync(BuildFilter(scopedQuery), scopedQuery, cancellationToken);
    }

    public async Task<LogStatistics> GetStatisticsAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        return await GetStatisticsInternalAsync(BuildFilter(query), cancellationToken);
    }

    public async Task<LogStatistics> GetStatisticsByUserAsync(string userId, LogQuery query, CancellationToken cancellationToken = default)
    {
        var scopedQuery = new LogQuery
        {
            SearchTerm = query.SearchTerm,
            PartnerId = userId,
            Action = query.Action,
            Source = query.Source,
            ActorIdentifier = query.ActorIdentifier,
            ActorIdentifierType = query.ActorIdentifierType,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc,
            Page = query.Page,
            PageSize = query.PageSize
        };

        return await GetStatisticsInternalAsync(BuildFilter(scopedQuery), cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, long>> GetActionCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, fromUtc)
            & Builders<ActivityLog>.Filter.Lt(log => log.CreatedAtUtc, toUtc)
            & Builders<ActivityLog>.Filter.Ne(log => log.Action, string.Empty);

        var logs = await _context.ActivityLogs.Find(filter)
            .Project(log => new ActivityLog
            {
                Action = log.Action
            })
            .ToListAsync(cancellationToken);

        return logs
            .Where(log => !string.IsNullOrWhiteSpace(log.Action))
            .GroupBy(log => log.Action, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.LongCount(), StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<AlertWarning>> GetUserActionCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, fromUtc)
            & Builders<ActivityLog>.Filter.Lt(log => log.CreatedAtUtc, toUtc)
            & Builders<ActivityLog>.Filter.Ne(log => log.Action, string.Empty);

        var logs = await _context.ActivityLogs.Find(filter)
            .Project(log => new ActivityLog
            {
                PartnerId = log.PartnerId,
                PartnerName = log.PartnerName,
                UserId = log.UserId,
                ExternalUserId = log.ExternalUserId,
                ActorIdentifier = log.ActorIdentifier,
                ActorIdentifierType = log.ActorIdentifierType,
                Source = log.Source,
                UserName = log.UserName,
                Action = log.Action
            })
            .ToListAsync(cancellationToken);

        return logs
            .Where(log => !string.IsNullOrWhiteSpace(log.Action))
            .Select(log => new
            {
                PartnerId = string.IsNullOrWhiteSpace(log.PartnerId) ? null : log.PartnerId.Trim(),
                PartnerName = string.IsNullOrWhiteSpace(log.PartnerName) ? "N/A" : log.PartnerName.Trim(),
                LegacyUserId = log.ExternalUserId,
                ActorIdentifier = ActorIdentityHelper.ResolveIdentifier(log),
                ActorIdentifierType = ActorIdentityHelper.ResolveType(log),
                UserName = string.IsNullOrWhiteSpace(log.UserName) ? "Anonymous" : log.UserName.Trim(),
                Action = log.Action.Trim(),
                NormalizedActorIdentifier = ActorIdentityHelper.ResolveIdentifier(log),
                NormalizedAction = log.Action.Trim().ToUpperInvariant()
            })
            .Where(log => !string.IsNullOrWhiteSpace(log.NormalizedActorIdentifier))
            .GroupBy(log => new
            {
                log.NormalizedActorIdentifier,
                log.NormalizedAction
            })
            .Select(group => new AlertWarning
            {
                PartnerId = group.Select(item => item.PartnerId).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)),
                PartnerName = group.Select(item => item.PartnerName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "N/A",
                UserId = group.Select(item => item.LegacyUserId).FirstOrDefault(id => id.HasValue),
                ActorIdentifier = group.Key.NormalizedActorIdentifier,
                ActorIdentifierType = group.Select(item => item.ActorIdentifierType).FirstOrDefault(type => !string.IsNullOrWhiteSpace(type)) ?? ActorIdentifierTypes.Unknown,
                UserName = group.First().UserName,
                Action = group.First().Action,
                CurrentCount = group.LongCount()
            })
            .OrderByDescending(item => item.CurrentCount)
            .ToList();
    }

    public async Task<IReadOnlyList<DailyAlertCount>> GetDailyUserActionCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, fromUtc)
            & Builders<ActivityLog>.Filter.Lt(log => log.CreatedAtUtc, toUtc)
            & Builders<ActivityLog>.Filter.Ne(log => log.Action, string.Empty);

        var logs = await _context.ActivityLogs.Find(filter)
            .Project(log => new ActivityLog
            {
                PartnerId = log.PartnerId,
                PartnerName = log.PartnerName,
                UserId = log.UserId,
                ExternalUserId = log.ExternalUserId,
                ActorIdentifier = log.ActorIdentifier,
                ActorIdentifierType = log.ActorIdentifierType,
                Source = log.Source,
                UserName = log.UserName,
                Action = log.Action,
                CreatedAtUtc = log.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        return logs
            .Where(log => !string.IsNullOrWhiteSpace(log.Action))
            .Select(log => new
            {
                AlertDateUtc = VietnamTimeExtensions.VietnamDateToUtcStart(log.CreatedAtUtc.ToVietnamTime().Date),
                OccurredAtUtc = log.CreatedAtUtc,
                PartnerId = string.IsNullOrWhiteSpace(log.PartnerId) ? null : log.PartnerId.Trim(),
                PartnerName = string.IsNullOrWhiteSpace(log.PartnerName) ? "N/A" : log.PartnerName.Trim(),
                UserId = log.ExternalUserId,
                ActorIdentifier = ActorIdentityHelper.ResolveIdentifier(log),
                ActorIdentifierType = ActorIdentityHelper.ResolveType(log),
                UserName = string.IsNullOrWhiteSpace(log.UserName) ? "Anonymous" : log.UserName.Trim(),
                Action = log.Action.Trim(),
                NormalizedAction = log.Action.Trim().ToUpperInvariant()
            })
            .Where(log => !string.IsNullOrWhiteSpace(log.ActorIdentifier))
            .GroupBy(log => new
            {
                log.AlertDateUtc,
                log.ActorIdentifier,
                log.NormalizedAction
            })
            .Select(group => new DailyAlertCount
            {
                AlertDateUtc = group.Key.AlertDateUtc,
                OccurredAtUtc = group.Max(item => item.OccurredAtUtc),
                PartnerId = group.Select(item => item.PartnerId).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)),
                PartnerName = group.Select(item => item.PartnerName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "N/A",
                UserId = group.Select(item => item.UserId).FirstOrDefault(id => id.HasValue),
                ActorIdentifier = group.Key.ActorIdentifier,
                ActorIdentifierType = group.Select(item => item.ActorIdentifierType).FirstOrDefault(type => !string.IsNullOrWhiteSpace(type)) ?? ActorIdentifierTypes.Unknown,
                UserName = group.Select(item => item.UserName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "Anonymous",
                Action = group.First().Action,
                CurrentCount = group.LongCount()
            })
            .OrderByDescending(item => item.AlertDateUtc)
            .ThenByDescending(item => item.CurrentCount)
            .ToList();
    }

    public Task<long> GetUserActionCountAsync(string actorIdentifier, string? actorIdentifierType, string action, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var builder = Builders<ActivityLog>.Filter;
        var normalizedActorIdentifier = ActorIdentityHelper.NormalizeIdentifier(actorIdentifier);
        var actorFilters = new List<FilterDefinition<ActivityLog>>
        {
            new BsonDocument("ActorIdentifier", normalizedActorIdentifier)
        };

        if (ActorIdentityHelper.TryGetLegacyExternalUserId(normalizedActorIdentifier, out var legacyUserId))
        {
            actorFilters.Add(new BsonDocument("ExternalUserId", legacyUserId));
        }

        var filter = builder.Or(actorFilters)
            & builder.Regex(log => log.Action, new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(action)}$", "i"))
            & Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, fromUtc)
            & Builders<ActivityLog>.Filter.Lt(log => log.CreatedAtUtc, toUtc);

        return _context.ActivityLogs.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, long>> GetActionCountsAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<ActivityLog>.Filter.Ne(log => log.Action, string.Empty);
        var logs = await _context.ActivityLogs.Find(filter)
            .Project(log => new ActivityLog
            {
                Action = log.Action
            })
            .ToListAsync(cancellationToken);

        return logs
            .Where(log => !string.IsNullOrWhiteSpace(log.Action))
            .GroupBy(log => log.Action, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.LongCount(), StringComparer.OrdinalIgnoreCase);
    }

    private async Task<PagedResult<ActivityLog>> GetPagedInternalAsync(
        FilterDefinition<ActivityLog> filter,
        LogQuery query,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Max(query.Page, 1);
        var normalizedPageSize = Math.Clamp(query.PageSize, 5, 100);
        var totalCount = await _context.ActivityLogs.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _context.ActivityLogs.Find(filter)
            .SortByDescending(log => log.CreatedAtUtc)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Limit(normalizedPageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ActivityLog>
        {
            Items = items,
            TotalCount = totalCount,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        };
    }

    private static FilterDefinition<ActivityLog> BuildFilter(LogQuery query)
    {
        var builder = Builders<ActivityLog>.Filter;
        var filters = new List<FilterDefinition<ActivityLog>>();

        if (!string.IsNullOrWhiteSpace(query.ActorIdentifier))
        {
            var normalizedActorIdentifier = ActorIdentityHelper.NormalizeIdentifier(query.ActorIdentifier);
            var actorFilters = new List<FilterDefinition<ActivityLog>>
            {
                new BsonDocument("ActorIdentifier", normalizedActorIdentifier)
            };

            if (ActorIdentityHelper.TryGetLegacyExternalUserId(normalizedActorIdentifier, out var legacyUserId))
            {
                actorFilters.Add(new BsonDocument("ExternalUserId", legacyUserId));
            }

            filters.Add(builder.Or(actorFilters));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            var searchFilters = new List<FilterDefinition<ActivityLog>>
            {
                new BsonDocument("ActorIdentifier", new BsonDocument("$regex", term).Add("$options", "i")),
                builder.Regex(log => log.UserName, new BsonRegularExpression(term, "i")),
                builder.Regex(log => log.PartnerName, new BsonRegularExpression(term, "i")),
                builder.Regex(log => log.Description, new BsonRegularExpression(term, "i")),
                builder.Regex(log => log.Endpoint, new BsonRegularExpression(term, "i"))
            };

            if (int.TryParse(term, out var legacyUserId))
            {
                searchFilters.Add(new BsonDocument("ExternalUserId", legacyUserId));
            }

            filters.Add(builder.Or(searchFilters));
        }

        if (!string.IsNullOrWhiteSpace(query.PartnerId))
        {
            filters.Add(builder.Eq(log => log.PartnerId, query.PartnerId));
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            filters.Add(builder.Eq(log => log.Action, query.Action));
        }

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            if (string.Equals(query.Source, ActivityLogSources.IntegratedApi, StringComparison.OrdinalIgnoreCase)
                || string.Equals(query.Source, ActivityLogSources.LegacyPartnerApi, StringComparison.OrdinalIgnoreCase))
            {
                filters.Add(builder.In(log => log.Source, new[]
                {
                    ActivityLogSources.IntegratedApi,
                    ActivityLogSources.LegacyPartnerApi
                }));
            }
            else
            {
                filters.Add(builder.Eq(log => log.Source, query.Source));
            }
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

    private async Task<LogStatistics> GetStatisticsInternalAsync(
        FilterDefinition<ActivityLog> filter,
        CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow.Date;
        var totalLogs = await _context.ActivityLogs.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var todayLogs = await _context.ActivityLogs.CountDocumentsAsync(
            filter & Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, today),
            cancellationToken: cancellationToken);
        var integratedLogs = await _context.ActivityLogs.CountDocumentsAsync(
            filter & Builders<ActivityLog>.Filter.In(log => log.Source, new[]
            {
                ActivityLogSources.IntegratedApi,
                ActivityLogSources.LegacyPartnerApi
            }),
            cancellationToken: cancellationToken);

        var statsLogs = await _context.ActivityLogs.Find(filter)
            .Project(log => new ActivityLog
            {
                UserId = log.UserId,
                ExternalUserId = log.ExternalUserId,
                ActorIdentifier = log.ActorIdentifier,
                ActorIdentifierType = log.ActorIdentifierType,
                UserName = log.UserName,
                PartnerName = log.PartnerName,
                Source = log.Source,
                Action = log.Action,
                CreatedAtUtc = log.CreatedAtUtc
            })
            .ToListAsync(cancellationToken);

        var chartDays = Enumerable.Range(0, 7)
            .Select(offset => today.AddDays(offset - 6))
            .ToList();

        var actionDailySeries = statsLogs
            .Where(log => !string.IsNullOrWhiteSpace(log.Action))
            .Select(log => new
            {
                PartnerName = string.IsNullOrWhiteSpace(log.PartnerName) ? "N/A" : log.PartnerName.Trim(),
                Action = log.Action.Trim(),
                CreatedAtUtc = log.CreatedAtUtc,
                NormalizedPartnerName = string.IsNullOrWhiteSpace(log.PartnerName) ? "N/A" : log.PartnerName.Trim().ToUpperInvariant(),
                NormalizedAction = log.Action.Trim().ToUpperInvariant()
            })
            .GroupBy(log => new
            {
                log.NormalizedPartnerName,
                log.NormalizedAction
            })
            .Select(group => new ActionTrendSeries
            {
                Action = $"{group.First().PartnerName} - {group.First().Action}",
                Values = chartDays
                    .Select(day => group.LongCount(log => log.CreatedAtUtc.Date == day))
                    .ToList()
            })
            .OrderByDescending(series => series.Values.Sum())
            .Take(5)
            .ToList();

        return new LogStatistics
        {
            TotalLogs = totalLogs,
            TodayLogs = todayLogs,
            UniqueUsers = statsLogs
                .Select(log =>
                {
                    var actorIdentifier = ActorIdentityHelper.ResolveIdentifier(log);
                    return string.IsNullOrWhiteSpace(actorIdentifier)
                        ? log.UserName
                        : actorIdentifier;
                })
                .Where(value => !string.IsNullOrWhiteSpace(value) && !string.Equals(value, "Anonymous", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .LongCount(),
            IntegratedLogs = integratedLogs,
            TopActions = statsLogs
                .Where(log => !string.IsNullOrWhiteSpace(log.Action))
                .GroupBy(log => log.Action)
                .Select(group => new BreakdownItem
                {
                    Label = group.Key,
                    Value = group.LongCount()
                })
                .OrderByDescending(item => item.Value)
                .Take(5)
                .ToList(),
            DailyActivity = chartDays
                .Select(day => new ChartPoint
                {
                    Label = day.ToString("yyyy-MM-dd"),
                    Value = statsLogs.LongCount(log => log.CreatedAtUtc.Date == day)
                })
                .ToList(),
            ActionDailySeries = actionDailySeries
        };
    }
}