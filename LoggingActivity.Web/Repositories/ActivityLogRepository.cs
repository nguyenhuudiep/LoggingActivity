using System.Globalization;
using LoggingActivity.Web.Data;
using LoggingActivity.Web.Infrastructure;
using LoggingActivity.Web.Models;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Driver;

namespace LoggingActivity.Web.Repositories;

public sealed class ActivityLogRepository : IActivityLogRepository
{
    private static readonly string[] IntegratedSources =
    [
        ActivityLogSources.IntegratedApi,
        ActivityLogSources.LegacyPartnerApi
    ];

    private readonly MongoDbContext _context;

    public ActivityLogRepository(MongoDbContext context)
    {
        _context = context;
    }

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<ActivityLog>(
                Builders<ActivityLog>.IndexKeys.Descending(log => log.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_activity_logs_created_at_desc" }),
            new CreateIndexModel<ActivityLog>(
                Builders<ActivityLog>.IndexKeys
                    .Ascending(log => log.PartnerId)
                    .Descending(log => log.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_activity_logs_partner_created_at" }),
            new CreateIndexModel<ActivityLog>(
                Builders<ActivityLog>.IndexKeys
                    .Ascending(log => log.Action)
                    .Descending(log => log.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_activity_logs_action_created_at" }),
            new CreateIndexModel<ActivityLog>(
                Builders<ActivityLog>.IndexKeys
                    .Ascending(log => log.ActorIdentifier)
                    .Ascending(log => log.Action)
                    .Descending(log => log.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_activity_logs_actor_action_created_at" }),
            new CreateIndexModel<ActivityLog>(
                Builders<ActivityLog>.IndexKeys
                    .Ascending(log => log.ExternalUserId)
                    .Ascending(log => log.Action)
                    .Descending(log => log.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_activity_logs_legacy_user_action_created_at" }),
            new CreateIndexModel<ActivityLog>(
                Builders<ActivityLog>.IndexKeys
                    .Ascending(log => log.Source)
                    .Descending(log => log.CreatedAtUtc),
                new CreateIndexOptions { Name = "ix_activity_logs_source_created_at" })
        };

        return _context.ActivityLogs.Indexes.CreateManyAsync(indexes, cancellationToken);
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

        return await AggregateActionCountsAsync(filter, cancellationToken);
    }

    public async Task<IReadOnlyList<AlertWarning>> GetUserActionCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, fromUtc)
            & Builders<ActivityLog>.Filter.Lt(log => log.CreatedAtUtc, toUtc)
            & Builders<ActivityLog>.Filter.Ne(log => log.Action, string.Empty);

        var pipeline = new[]
        {
            BuildMatchStage(filter),
            new BsonDocument("$project", new BsonDocument
            {
                { "PartnerId", "$PartnerId" },
                { "PartnerName", BuildNormalizedTextExpression("PartnerName", "N/A") },
                { "UserId", "$ExternalUserId" },
                { "ActorIdentifier", BuildResolvedActorIdentifierExpression() },
                { "ActorIdentifierType", BuildNormalizedTextExpression("ActorIdentifierType", ActorIdentifierTypes.Unknown) },
                { "UserName", BuildNormalizedTextExpression("UserName", "Anonymous") },
                { "Action", BuildNormalizedTextExpression("Action", string.Empty) }
            }),
            new BsonDocument("$match", new BsonDocument
            {
                { "Action", new BsonDocument("$ne", string.Empty) },
                { "$nor", new BsonArray
                {
                    new BsonDocument("ActorIdentifier", BsonNull.Value),
                    new BsonDocument("ActorIdentifier", string.Empty)
                }}
            }),
            new BsonDocument("$addFields", new BsonDocument
            {
                { "NormalizedActorIdentifier", new BsonDocument("$toUpper", "$ActorIdentifier") },
                { "NormalizedAction", new BsonDocument("$toUpper", "$Action") }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                {
                    "_id",
                    new BsonDocument
                    {
                        { "actor", "$NormalizedActorIdentifier" },
                        { "action", "$NormalizedAction" }
                    }
                },
                { "PartnerId", new BsonDocument("$first", "$PartnerId") },
                { "PartnerName", new BsonDocument("$first", "$PartnerName") },
                { "UserId", new BsonDocument("$first", "$UserId") },
                { "ActorIdentifier", new BsonDocument("$first", "$ActorIdentifier") },
                { "ActorIdentifierType", new BsonDocument("$first", "$ActorIdentifierType") },
                { "UserName", new BsonDocument("$first", "$UserName") },
                { "Action", new BsonDocument("$first", "$Action") },
                { "CurrentCount", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument("CurrentCount", -1L))
        };

        var docs = await _context.ActivityLogs.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        return docs.Select(ToAlertWarning).ToList();
    }

    public async Task<IReadOnlyList<DailyAlertCount>> GetDailyUserActionCountsAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, fromUtc)
            & Builders<ActivityLog>.Filter.Lt(log => log.CreatedAtUtc, toUtc)
            & Builders<ActivityLog>.Filter.Ne(log => log.Action, string.Empty);

        var pipeline = new[]
        {
            BuildMatchStage(filter),
            new BsonDocument("$project", new BsonDocument
            {
                { "CreatedAtUtc", "$CreatedAtUtc" },
                { "AlertDateLocal", new BsonDocument("$dateToString", new BsonDocument
                    {
                        { "date", "$CreatedAtUtc" },
                        { "format", "%Y-%m-%d" },
                        { "timezone", "Asia/Ho_Chi_Minh" }
                    })
                },
                { "PartnerId", "$PartnerId" },
                { "PartnerName", BuildNormalizedTextExpression("PartnerName", "N/A") },
                { "UserId", "$ExternalUserId" },
                { "ActorIdentifier", BuildResolvedActorIdentifierExpression() },
                { "ActorIdentifierType", BuildNormalizedTextExpression("ActorIdentifierType", ActorIdentifierTypes.Unknown) },
                { "UserName", BuildNormalizedTextExpression("UserName", "Anonymous") },
                { "Action", BuildNormalizedTextExpression("Action", string.Empty) }
            }),
            new BsonDocument("$match", new BsonDocument
            {
                { "Action", new BsonDocument("$ne", string.Empty) },
                { "$nor", new BsonArray
                {
                    new BsonDocument("ActorIdentifier", BsonNull.Value),
                    new BsonDocument("ActorIdentifier", string.Empty)
                }}
            }),
            new BsonDocument("$addFields", new BsonDocument
            {
                { "NormalizedActorIdentifier", new BsonDocument("$toUpper", "$ActorIdentifier") },
                { "NormalizedAction", new BsonDocument("$toUpper", "$Action") }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                {
                    "_id",
                    new BsonDocument
                    {
                        { "date", "$AlertDateLocal" },
                        { "actor", "$NormalizedActorIdentifier" },
                        { "action", "$NormalizedAction" }
                    }
                },
                { "OccurredAtUtc", new BsonDocument("$max", "$CreatedAtUtc") },
                { "PartnerId", new BsonDocument("$first", "$PartnerId") },
                { "PartnerName", new BsonDocument("$first", "$PartnerName") },
                { "UserId", new BsonDocument("$first", "$UserId") },
                { "ActorIdentifier", new BsonDocument("$first", "$ActorIdentifier") },
                { "ActorIdentifierType", new BsonDocument("$first", "$ActorIdentifierType") },
                { "UserName", new BsonDocument("$first", "$UserName") },
                { "Action", new BsonDocument("$first", "$Action") },
                { "CurrentCount", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument
            {
                { "_id.date", -1L },
                { "CurrentCount", -1L }
            })
        };

        var docs = await _context.ActivityLogs.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);

        var results = new List<DailyAlertCount>(docs.Count);
        foreach (var doc in docs)
        {
            var id = doc["_id"].AsBsonDocument;
            var localDateText = GetTrimmedString(id, "date");
            var localDate = DateTime.ParseExact(localDateText, "yyyy-MM-dd", CultureInfo.InvariantCulture);

            results.Add(new DailyAlertCount
            {
                AlertDateUtc = VietnamTimeExtensions.VietnamDateToUtcStart(localDate),
                OccurredAtUtc = GetDateTimeUtc(doc, "OccurredAtUtc"),
                PartnerId = GetNullableString(doc, "PartnerId"),
                PartnerName = GetTrimmedString(doc, "PartnerName", "N/A"),
                UserId = GetNullableInt(doc, "UserId"),
                ActorIdentifier = GetTrimmedString(doc, "ActorIdentifier"),
                ActorIdentifierType = ActorIdentityHelper.NormalizeType(GetTrimmedString(doc, "ActorIdentifierType", ActorIdentifierTypes.Unknown), GetTrimmedString(doc, "ActorIdentifier")),
                UserName = GetTrimmedString(doc, "UserName", "Anonymous"),
                Action = GetTrimmedString(doc, "Action"),
                CurrentCount = GetInt64(doc, "CurrentCount")
            });
        }

        return results;
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

    public Task<long> GetPartnerUserActionCountAsync(
        string partnerId,
        string actorIdentifier,
        string? actorIdentifierType,
        string action,
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken cancellationToken = default)
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

        var filter = builder.Eq(log => log.PartnerId, partnerId)
            & builder.Or(actorFilters)
            & builder.Regex(log => log.Action, new BsonRegularExpression($"^{System.Text.RegularExpressions.Regex.Escape(action)}$", "i"))
            & builder.Gte(log => log.CreatedAtUtc, fromUtc)
            & builder.Lt(log => log.CreatedAtUtc, toUtc);

        return _context.ActivityLogs.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, long>> GetActionCountsAsync(CancellationToken cancellationToken = default)
    {
        var filter = Builders<ActivityLog>.Filter.Ne(log => log.Action, string.Empty);
        return await AggregateActionCountsAsync(filter, cancellationToken);
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
                filters.Add(builder.In(log => log.Source, IntegratedSources));
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
        var chartDays = Enumerable.Range(0, 7)
            .Select(offset => today.AddDays(offset - 6))
            .ToList();

        var totalLogsTask = _context.ActivityLogs.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var todayLogsTask = _context.ActivityLogs.CountDocumentsAsync(
            filter & Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, today),
            cancellationToken: cancellationToken);
        var integratedLogsTask = _context.ActivityLogs.CountDocumentsAsync(
            filter & Builders<ActivityLog>.Filter.In(log => log.Source, IntegratedSources),
            cancellationToken: cancellationToken);
        var uniqueUsersTask = GetUniqueUsersCountAsync(filter, cancellationToken);
        var topActionsTask = GetTopActionsAsync(filter, cancellationToken);
        var topActionsTodayTask = GetTopActionsTodayAsync(filter, cancellationToken);
        var topUsersTodayTask = GetTopUsersTodayAsync(filter, cancellationToken);
        var dailyActivityTask = GetDailyActivityAsync(filter, chartDays, cancellationToken);
        var hourlyActivityTask = GetHourlyActivityAsync(filter, cancellationToken);
        var actionTrendSeriesTask = GetActionTrendSeriesAsync(filter, chartDays, cancellationToken);

        await Task.WhenAll(
            totalLogsTask,
            todayLogsTask,
            integratedLogsTask,
            uniqueUsersTask,
            topActionsTask,
            topActionsTodayTask,
            topUsersTodayTask,
            dailyActivityTask,
            hourlyActivityTask,
            actionTrendSeriesTask);

        return new LogStatistics
        {
            TotalLogs = totalLogsTask.Result,
            TodayLogs = todayLogsTask.Result,
            UniqueUsers = uniqueUsersTask.Result,
            IntegratedLogs = integratedLogsTask.Result,
            TopActions = topActionsTask.Result,
            TopActionsToday = topActionsTodayTask.Result,
            TopUsersToday = topUsersTodayTask.Result,
            DailyActivity = dailyActivityTask.Result,
            HourlyActivity = hourlyActivityTask.Result,
            ActionDailySeries = actionTrendSeriesTask.Result
        };
    }

    private async Task<IReadOnlyDictionary<string, long>> AggregateActionCountsAsync(
        FilterDefinition<ActivityLog> filter,
        CancellationToken cancellationToken)
    {
        var pipeline = new[]
        {
            BuildMatchStage(filter),
            new BsonDocument("$project", new BsonDocument
            {
                { "Action", BuildNormalizedTextExpression("Action", string.Empty) }
            }),
            new BsonDocument("$match", new BsonDocument("Action", new BsonDocument("$ne", string.Empty))),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", new BsonDocument("$toUpper", "$Action") },
                { "Count", new BsonDocument("$sum", 1) }
            })
        };

        var docs = await _context.ActivityLogs.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        return docs.ToDictionary(
            doc => doc["_id"].AsString,
            doc => GetInt64(doc, "Count"),
            StringComparer.OrdinalIgnoreCase);
    }

    private async Task<long> GetUniqueUsersCountAsync(FilterDefinition<ActivityLog> filter, CancellationToken cancellationToken)
    {
        var pipeline = new[]
        {
            BuildMatchStage(filter),
            new BsonDocument("$project", new BsonDocument
            {
                { "ResolvedIdentifier", BuildResolvedActorOrUserExpression() }
            }),
            new BsonDocument("$match", new BsonDocument
            {
                { "$nor", new BsonArray
                {
                    new BsonDocument("ResolvedIdentifier", BsonNull.Value),
                    new BsonDocument("ResolvedIdentifier", string.Empty)
                }}
            }),
            new BsonDocument("$group", new BsonDocument("_id", new BsonDocument("$toUpper", "$ResolvedIdentifier"))),
            new BsonDocument("$match", new BsonDocument("_id", new BsonDocument("$ne", "ANONYMOUS"))),
            new BsonDocument("$count", "Value")
        };

        var docs = await _context.ActivityLogs.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        return docs.Count == 0 ? 0 : GetInt64(docs[0], "Value");
    }

    private async Task<IReadOnlyList<BreakdownItem>> GetTopActionsAsync(FilterDefinition<ActivityLog> filter, CancellationToken cancellationToken)
    {
        var pipeline = new[]
        {
            BuildMatchStage(filter),
            new BsonDocument("$project", new BsonDocument
            {
                { "Action", BuildNormalizedTextExpression("Action", string.Empty) }
            }),
            new BsonDocument("$match", new BsonDocument("Action", new BsonDocument("$ne", string.Empty))),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$Action" },
                { "Value", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument("Value", -1L)),
            new BsonDocument("$limit", 5)
        };

        var docs = await _context.ActivityLogs.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        return docs.Select(doc => new BreakdownItem
        {
            Label = GetTrimmedString(doc, "_id"),
            Value = GetInt64(doc, "Value")
        }).ToList();
    }

    private async Task<IReadOnlyList<BreakdownItem>> GetTopActionsTodayAsync(
        FilterDefinition<ActivityLog> filter,
        CancellationToken cancellationToken)
    {
        var todayVietnam = VietnamTimeExtensions.TodayInVietnamDate();
        var startUtc = VietnamTimeExtensions.VietnamDateToUtcStart(todayVietnam);
        var endUtc = VietnamTimeExtensions.VietnamDateToUtcStart(todayVietnam.AddDays(1));

        var scopedFilter = filter
            & Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, startUtc)
            & Builders<ActivityLog>.Filter.Lt(log => log.CreatedAtUtc, endUtc);

        return await GetTopActionsAsync(scopedFilter, cancellationToken);
    }

    private async Task<IReadOnlyList<TopUserActionSummary>> GetTopUsersTodayAsync(
        FilterDefinition<ActivityLog> filter,
        CancellationToken cancellationToken)
    {
        var todayVietnam = VietnamTimeExtensions.TodayInVietnamDate();
        var startUtc = VietnamTimeExtensions.VietnamDateToUtcStart(todayVietnam);
        var endUtc = VietnamTimeExtensions.VietnamDateToUtcStart(todayVietnam.AddDays(1));

        var scopedFilter = filter
            & Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, startUtc)
            & Builders<ActivityLog>.Filter.Lt(log => log.CreatedAtUtc, endUtc);

        var pipeline = new[]
        {
            BuildMatchStage(scopedFilter),
            new BsonDocument("$project", new BsonDocument
            {
                { "ActorIdentifier", BuildResolvedActorIdentifierExpression() },
                { "UserName", BuildNormalizedTextExpression("UserName", "Anonymous") },
                { "Action", BuildNormalizedTextExpression("Action", string.Empty) }
            }),
            new BsonDocument("$match", new BsonDocument
            {
                { "Action", new BsonDocument("$ne", string.Empty) },
                { "$nor", new BsonArray
                {
                    new BsonDocument("ActorIdentifier", BsonNull.Value),
                    new BsonDocument("ActorIdentifier", string.Empty)
                }}
            }),
            new BsonDocument("$addFields", new BsonDocument
            {
                { "NormalizedActorIdentifier", new BsonDocument("$toUpper", "$ActorIdentifier") },
                { "NormalizedAction", new BsonDocument("$toUpper", "$Action") }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                {
                    "_id",
                    new BsonDocument
                    {
                        { "actor", "$NormalizedActorIdentifier" },
                        { "action", "$NormalizedAction" }
                    }
                },
                { "ActorIdentifier", new BsonDocument("$first", "$ActorIdentifier") },
                { "UserName", new BsonDocument("$first", "$UserName") },
                { "Action", new BsonDocument("$first", "$Action") },
                { "ActionCount", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$sort", new BsonDocument
            {
                { "_id.actor", 1L },
                { "ActionCount", -1L },
                { "Action", 1L }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$_id.actor" },
                { "ActorIdentifier", new BsonDocument("$first", "$ActorIdentifier") },
                { "UserName", new BsonDocument("$first", "$UserName") },
                { "TopAction", new BsonDocument("$first", "$Action") },
                { "TopActionCount", new BsonDocument("$first", "$ActionCount") },
                { "TotalActions", new BsonDocument("$sum", "$ActionCount") }
            }),
            new BsonDocument("$sort", new BsonDocument("TotalActions", -1L)),
            new BsonDocument("$limit", 5)
        };

        var docs = await _context.ActivityLogs.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        return docs.Select(doc =>
        {
            return new TopUserActionSummary
            {
                ActorIdentifier = GetTrimmedString(doc, "ActorIdentifier"),
                UserName = GetTrimmedString(doc, "UserName", "Anonymous"),
                TotalActions = GetInt64(doc, "TotalActions"),
                TopAction = GetTrimmedString(doc, "TopAction"),
                TopActionCount = GetInt64(doc, "TopActionCount")
            };
        }).ToList();
    }

    private async Task<IReadOnlyList<ChartPoint>> GetDailyActivityAsync(
        FilterDefinition<ActivityLog> filter,
        IReadOnlyList<DateTime> chartDays,
        CancellationToken cancellationToken)
    {
        var startDate = chartDays[0];
        var endDate = chartDays[^1].AddDays(1);

        var scopedFilter = filter
            & Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, startDate)
            & Builders<ActivityLog>.Filter.Lt(log => log.CreatedAtUtc, endDate);

        var pipeline = new[]
        {
            BuildMatchStage(scopedFilter),
            new BsonDocument("$project", new BsonDocument
            {
                {
                    "Day",
                    new BsonDocument("$dateToString", new BsonDocument
                    {
                        { "date", "$CreatedAtUtc" },
                        { "format", "%Y-%m-%d" },
                        { "timezone", "UTC" }
                    })
                }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$Day" },
                { "Value", new BsonDocument("$sum", 1) }
            })
        };

        var docs = await _context.ActivityLogs.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        var valuesByDay = docs.ToDictionary(
            doc => GetTrimmedString(doc, "_id"),
            doc => GetInt64(doc, "Value"),
            StringComparer.OrdinalIgnoreCase);

        return chartDays
            .Select(day =>
            {
                var key = day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
                valuesByDay.TryGetValue(key, out var value);
                return new ChartPoint
                {
                    Label = key,
                    Value = value
                };
            })
            .ToList();
    }

    private async Task<IReadOnlyList<ChartPoint>> GetHourlyActivityAsync(
        FilterDefinition<ActivityLog> filter,
        CancellationToken cancellationToken)
    {
        var todayVietnam = VietnamTimeExtensions.TodayInVietnamDate();
        var startUtc = VietnamTimeExtensions.VietnamDateToUtcStart(todayVietnam);
        var endUtc = VietnamTimeExtensions.VietnamDateToUtcStart(todayVietnam.AddDays(1));

        var scopedFilter = filter
            & Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, startUtc)
            & Builders<ActivityLog>.Filter.Lt(log => log.CreatedAtUtc, endUtc);

        var pipeline = new[]
        {
            BuildMatchStage(scopedFilter),
            new BsonDocument("$project", new BsonDocument
            {
                {
                    "Hour",
                    new BsonDocument("$dateToString", new BsonDocument
                    {
                        { "date", "$CreatedAtUtc" },
                        { "format", "%H" },
                        { "timezone", "Asia/Ho_Chi_Minh" }
                    })
                }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                { "_id", "$Hour" },
                { "Value", new BsonDocument("$sum", 1) }
            })
        };

        var docs = await _context.ActivityLogs.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        var valuesByHour = docs.ToDictionary(
            doc => GetTrimmedString(doc, "_id"),
            doc => GetInt64(doc, "Value"),
            StringComparer.OrdinalIgnoreCase);

        return Enumerable.Range(0, 24)
            .Select(hour =>
            {
                var key = hour.ToString("00", CultureInfo.InvariantCulture);
                valuesByHour.TryGetValue(key, out var value);
                return new ChartPoint
                {
                    Label = $"{key}:00",
                    Value = value
                };
            })
            .ToList();
    }

    private async Task<IReadOnlyList<ActionTrendSeries>> GetActionTrendSeriesAsync(
        FilterDefinition<ActivityLog> filter,
        IReadOnlyList<DateTime> chartDays,
        CancellationToken cancellationToken)
    {
        var startDate = chartDays[0];
        var endDate = chartDays[^1].AddDays(1);

        var scopedFilter = filter
            & Builders<ActivityLog>.Filter.Gte(log => log.CreatedAtUtc, startDate)
            & Builders<ActivityLog>.Filter.Lt(log => log.CreatedAtUtc, endDate)
            & Builders<ActivityLog>.Filter.Ne(log => log.Action, string.Empty);

        var pipeline = new[]
        {
            BuildMatchStage(scopedFilter),
            new BsonDocument("$project", new BsonDocument
            {
                { "PartnerName", BuildNormalizedTextExpression("PartnerName", "N/A") },
                { "Action", BuildNormalizedTextExpression("Action", string.Empty) },
                {
                    "Day",
                    new BsonDocument("$dateToString", new BsonDocument
                    {
                        { "date", "$CreatedAtUtc" },
                        { "format", "%Y-%m-%d" },
                        { "timezone", "UTC" }
                    })
                }
            }),
            new BsonDocument("$match", new BsonDocument("Action", new BsonDocument("$ne", string.Empty))),
            new BsonDocument("$addFields", new BsonDocument
            {
                { "PartnerNameNormalized", new BsonDocument("$toUpper", "$PartnerName") },
                { "ActionNormalized", new BsonDocument("$toUpper", "$Action") }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                {
                    "_id",
                    new BsonDocument
                    {
                        { "PartnerNameNormalized", "$PartnerNameNormalized" },
                        { "ActionNormalized", "$ActionNormalized" },
                        { "Day", "$Day" }
                    }
                },
                { "PartnerName", new BsonDocument("$first", "$PartnerName") },
                { "Action", new BsonDocument("$first", "$Action") },
                { "Value", new BsonDocument("$sum", 1) }
            }),
            new BsonDocument("$group", new BsonDocument
            {
                {
                    "_id",
                    new BsonDocument
                    {
                        { "PartnerNameNormalized", "$_id.PartnerNameNormalized" },
                        { "ActionNormalized", "$_id.ActionNormalized" }
                    }
                },
                { "PartnerName", new BsonDocument("$first", "$PartnerName") },
                { "Action", new BsonDocument("$first", "$Action") },
                { "Total", new BsonDocument("$sum", "$Value") },
                {
                    "Daily",
                    new BsonDocument("$push", new BsonDocument
                    {
                        { "Day", "$_id.Day" },
                        { "Value", "$Value" }
                    })
                }
            }),
            new BsonDocument("$sort", new BsonDocument("Total", -1L)),
            new BsonDocument("$limit", 5)
        };

        var docs = await _context.ActivityLogs.Aggregate<BsonDocument>(pipeline).ToListAsync(cancellationToken);
        var dayLabels = chartDays
            .Select(day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            .ToList();

        var result = new List<ActionTrendSeries>(docs.Count);
        foreach (var doc in docs)
        {
            var dailyMap = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (doc.TryGetValue("Daily", out var dailyValue) && dailyValue.IsBsonArray)
            {
                foreach (var item in dailyValue.AsBsonArray)
                {
                    var dailyEntry = item.AsBsonDocument;
                    dailyMap[GetTrimmedString(dailyEntry, "Day")] = GetInt64(dailyEntry, "Value");
                }
            }

            result.Add(new ActionTrendSeries
            {
                Action = $"{GetTrimmedString(doc, "PartnerName", "N/A")} - {GetTrimmedString(doc, "Action")}",
                Values = dayLabels.Select(label => dailyMap.TryGetValue(label, out var value) ? value : 0).ToList()
            });
        }

        return result;
    }

    private static AlertWarning ToAlertWarning(BsonDocument doc)
    {
        var actorIdentifier = GetTrimmedString(doc, "ActorIdentifier");
        return new AlertWarning
        {
            PartnerId = GetNullableString(doc, "PartnerId"),
            PartnerName = GetTrimmedString(doc, "PartnerName", "N/A"),
            UserId = GetNullableInt(doc, "UserId"),
            ActorIdentifier = actorIdentifier,
            ActorIdentifierType = ActorIdentityHelper.NormalizeType(GetTrimmedString(doc, "ActorIdentifierType", ActorIdentifierTypes.Unknown), actorIdentifier),
            UserName = GetTrimmedString(doc, "UserName", "Anonymous"),
            Action = GetTrimmedString(doc, "Action"),
            CurrentCount = GetInt64(doc, "CurrentCount")
        };
    }

    private BsonDocument BuildMatchStage(FilterDefinition<ActivityLog> filter)
    {
        var serializer = BsonSerializer.SerializerRegistry.GetSerializer<ActivityLog>();
        var renderedFilter = filter.Render(new RenderArgs<ActivityLog>(serializer, BsonSerializer.SerializerRegistry));
        return new BsonDocument("$match", renderedFilter);
    }

    private static BsonDocument BuildNormalizedTextExpression(string field, string fallback)
    {
        return new BsonDocument("$let", new BsonDocument
        {
            {
                "vars",
                new BsonDocument("value", new BsonDocument("$trim", new BsonDocument("input", new BsonDocument("$ifNull", new BsonArray { $"${field}", string.Empty }))))
            },
            {
                "in",
                new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$eq", new BsonArray { "$$value", string.Empty }),
                    fallback,
                    "$$value"
                })
            }
        });
    }

    private static BsonDocument BuildResolvedActorIdentifierExpression()
    {
        return new BsonDocument("$let", new BsonDocument
        {
            {
                "vars",
                new BsonDocument
                {
                    { "actor", new BsonDocument("$trim", new BsonDocument("input", new BsonDocument("$ifNull", new BsonArray { "$ActorIdentifier", string.Empty }))) },
                    {
                        "legacy",
                        new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$ExternalUserId", BsonNull.Value }),
                            string.Empty,
                            new BsonDocument("$toString", "$ExternalUserId")
                        })
                    }
                }
            },
            {
                "in",
                new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$ne", new BsonArray { "$$actor", string.Empty }),
                    "$$actor",
                    new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$ne", new BsonArray { "$$legacy", string.Empty }),
                        "$$legacy",
                        BsonNull.Value
                    })
                })
            }
        });
    }

    private static BsonDocument BuildResolvedActorOrUserExpression()
    {
        return new BsonDocument("$let", new BsonDocument
        {
            {
                "vars",
                new BsonDocument
                {
                    { "actor", new BsonDocument("$trim", new BsonDocument("input", new BsonDocument("$ifNull", new BsonArray { "$ActorIdentifier", string.Empty }))) },
                    {
                        "legacy",
                        new BsonDocument("$cond", new BsonArray
                        {
                            new BsonDocument("$eq", new BsonArray { "$ExternalUserId", BsonNull.Value }),
                            string.Empty,
                            new BsonDocument("$toString", "$ExternalUserId")
                        })
                    },
                    { "userName", new BsonDocument("$trim", new BsonDocument("input", new BsonDocument("$ifNull", new BsonArray { "$UserName", string.Empty }))) }
                }
            },
            {
                "in",
                new BsonDocument("$cond", new BsonArray
                {
                    new BsonDocument("$ne", new BsonArray { "$$actor", string.Empty }),
                    "$$actor",
                    new BsonDocument("$cond", new BsonArray
                    {
                        new BsonDocument("$ne", new BsonArray { "$$legacy", string.Empty }),
                        "$$legacy",
                        "$$userName"
                    })
                })
            }
        });
    }

    private static string GetTrimmedString(BsonDocument doc, string key, string fallback = "")
    {
        if (!doc.TryGetValue(key, out var value) || value.IsBsonNull)
        {
            return fallback;
        }

        var text = (value.ToString() ?? string.Empty).Trim();
        return string.IsNullOrWhiteSpace(text) ? fallback : text;
    }

    private static string? GetNullableString(BsonDocument doc, string key)
    {
        var value = GetTrimmedString(doc, key);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }

    private static int? GetNullableInt(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var value) || value.IsBsonNull)
        {
            return null;
        }

        if (value.IsInt32)
        {
            return value.AsInt32;
        }

        if (value.IsInt64)
        {
            return (int)value.AsInt64;
        }

        if (int.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return null;
    }

    private static long GetInt64(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var value) || value.IsBsonNull)
        {
            return 0;
        }

        if (value.IsInt64)
        {
            return value.AsInt64;
        }

        if (value.IsInt32)
        {
            return value.AsInt32;
        }

        if (long.TryParse(value.ToString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static DateTime GetDateTimeUtc(BsonDocument doc, string key)
    {
        if (!doc.TryGetValue(key, out var value) || value.IsBsonNull)
        {
            return DateTime.UtcNow;
        }

        if (value.IsValidDateTime)
        {
            return value.ToUniversalTime();
        }

        if (DateTime.TryParse(value.ToString(), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return parsed;
        }

        return DateTime.UtcNow;
    }
}
