using LoggingActivity.Web.Data;
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
        var filter = BuildFilter(query, null);
        return await GetPagedInternalAsync(filter, query, cancellationToken);
    }

    public async Task<PagedResult<ActivityLog>> GetPagedByUserAsync(string userId, LogQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query, userId);
        return await GetPagedInternalAsync(filter, query, cancellationToken);
    }

    public async Task<LogStatistics> GetStatisticsAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        return await GetStatisticsInternalAsync(BuildFilter(query, null), cancellationToken);
    }

    public async Task<LogStatistics> GetStatisticsByUserAsync(string userId, LogQuery query, CancellationToken cancellationToken = default)
    {
        return await GetStatisticsInternalAsync(BuildFilter(query, userId), cancellationToken);
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
                ExternalUserId = log.ExternalUserId,
                UserName = log.UserName,
                Action = log.Action
            })
            .ToListAsync(cancellationToken);

        return logs
            .Where(log => !string.IsNullOrWhiteSpace(log.Action) && log.ExternalUserId.HasValue)
            .Select(log => new
            {
                PartnerId = string.IsNullOrWhiteSpace(log.PartnerId) ? null : log.PartnerId.Trim(),
                PartnerName = string.IsNullOrWhiteSpace(log.PartnerName) ? "N/A" : log.PartnerName.Trim(),
                ExternalUserId = log.ExternalUserId,
                UserName = string.IsNullOrWhiteSpace(log.UserName) ? "Anonymous" : log.UserName.Trim(),
                Action = log.Action.Trim(),
                NormalizedExternalUserId = log.ExternalUserId ?? 0,
                NormalizedAction = log.Action.Trim().ToUpperInvariant()
            })
            .GroupBy(log => new
            {
                log.NormalizedExternalUserId,
                log.NormalizedAction
            })
            .Select(group => new AlertWarning
            {
                PartnerId = group.Select(item => item.PartnerId).FirstOrDefault(id => !string.IsNullOrWhiteSpace(id)),
                PartnerName = group.Select(item => item.PartnerName).FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)) ?? "N/A",
                UserId = group.First().ExternalUserId,
                UserName = group.First().UserName,
                Action = group.First().Action,
                CurrentCount = group.LongCount()
            })
            .OrderByDescending(item => item.CurrentCount)
            .ToList();
    }

    public Task<long> GetUserActionCountAsync(int userId, string action, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var filter = Builders<ActivityLog>.Filter.Eq(log => log.ExternalUserId, userId)
            & Builders<ActivityLog>.Filter.Eq(log => log.Action, action)
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

    private static FilterDefinition<ActivityLog> BuildFilter(LogQuery query, string? userId)
    {
        var builder = Builders<ActivityLog>.Filter;
        var filters = new List<FilterDefinition<ActivityLog>>();

        if (!string.IsNullOrWhiteSpace(userId))
        {
            filters.Add(builder.Eq(log => log.UserId, userId));
        }

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var term = query.SearchTerm.Trim();
            filters.Add(builder.Or(
                builder.Regex(log => log.UserName, new BsonRegularExpression(term, "i")),
                builder.Regex(log => log.PartnerName, new BsonRegularExpression(term, "i")),
                builder.Regex(log => log.Description, new BsonRegularExpression(term, "i")),
                builder.Regex(log => log.Endpoint, new BsonRegularExpression(term, "i"))));
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
                ExternalUserId = log.ExternalUserId,
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
                .Where(log => !string.Equals(log.UserName, "Anonymous", StringComparison.OrdinalIgnoreCase))
                .Select(log => log.UserName)
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