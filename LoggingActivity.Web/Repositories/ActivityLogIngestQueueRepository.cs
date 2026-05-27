using LoggingActivity.Web.Data;
using LoggingActivity.Web.Models;
using MongoDB.Driver;

namespace LoggingActivity.Web.Repositories;

public sealed class ActivityLogIngestQueueRepository : IActivityLogIngestQueueRepository
{
    private readonly MongoDbContext _context;

    public ActivityLogIngestQueueRepository(MongoDbContext context)
    {
        _context = context;
    }

    public async Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var dedupIndex = new CreateIndexModel<ActivityLogIngestQueueItem>(
            Builders<ActivityLogIngestQueueItem>.IndexKeys.Ascending(item => item.DeduplicationKey),
            new CreateIndexOptions { Unique = true, Name = "uq_ingest_queue_dedup" });

        var processingIndex = new CreateIndexModel<ActivityLogIngestQueueItem>(
            Builders<ActivityLogIngestQueueItem>.IndexKeys
                .Ascending(item => item.Status)
                .Ascending(item => item.AvailableAtUtc)
                .Ascending(item => item.LeaseExpiresAtUtc),
            new CreateIndexOptions { Name = "ix_ingest_queue_status_schedule" });

        await _context.ActivityLogIngestQueue.Indexes.CreateManyAsync(new[] { dedupIndex, processingIndex }, cancellationToken);
    }

    public async Task<bool> EnqueueAsync(ActivityLogIngestQueueItem item, CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.ActivityLogIngestQueue.InsertOneAsync(item, cancellationToken: cancellationToken);
            return true;
        }
        catch (MongoWriteException ex) when (ex.WriteError?.Category == ServerErrorCategory.DuplicateKey)
        {
            return false;
        }
    }

    public async Task<long> DeleteDemoAsync(CancellationToken cancellationToken = default)
    {
        var builder = Builders<ActivityLogIngestQueueItem>.Filter;
        var filter = builder.Or(
            builder.Regex(item => item.RequestId, new MongoDB.Bson.BsonRegularExpression("^demo-", "i")),
            builder.Regex(item => item.Action, new MongoDB.Bson.BsonRegularExpression("^DEMO_", "i")));

        var result = await _context.ActivityLogIngestQueue.DeleteManyAsync(filter, cancellationToken);
        return result.DeletedCount;
    }

    public async Task<PagedResult<ActivityLogIngestQueueItem>> GetPagedAsync(ActivityLogIngestQueueQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);
        var totalCount = await _context.ActivityLogIngestQueue.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var page = Math.Max(query.Page, 1);
        var pageSize = Math.Clamp(query.PageSize, 1, 100);
        var items = await _context.ActivityLogIngestQueue
            .Find(filter)
            .SortByDescending(item => item.ReceivedAtUtc)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<ActivityLogIngestQueueItem>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<ActivityLogIngestQueueSummary> GetSummaryAsync(ActivityLogIngestQueueQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);
        var total = await _context.ActivityLogIngestQueue.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var pending = await _context.ActivityLogIngestQueue.CountDocumentsAsync(
            filter & Builders<ActivityLogIngestQueueItem>.Filter.Eq(item => item.Status, ActivityLogIngestQueueStatuses.Pending),
            cancellationToken: cancellationToken);
        var processing = await _context.ActivityLogIngestQueue.CountDocumentsAsync(
            filter & Builders<ActivityLogIngestQueueItem>.Filter.Eq(item => item.Status, ActivityLogIngestQueueStatuses.Processing),
            cancellationToken: cancellationToken);
        var failed = await _context.ActivityLogIngestQueue.CountDocumentsAsync(
            filter & Builders<ActivityLogIngestQueueItem>.Filter.Eq(item => item.Status, ActivityLogIngestQueueStatuses.Failed),
            cancellationToken: cancellationToken);
        var completed = await _context.ActivityLogIngestQueue.CountDocumentsAsync(
            filter & Builders<ActivityLogIngestQueueItem>.Filter.Eq(item => item.Status, ActivityLogIngestQueueStatuses.Completed),
            cancellationToken: cancellationToken);

        return new ActivityLogIngestQueueSummary
        {
            TotalCount = total,
            PendingCount = pending,
            ProcessingCount = processing,
            FailedCount = failed,
            CompletedCount = completed
        };
    }

    public async Task<ActivityLogIngestQueueItem?> TryLeaseNextAsync(string workerId, DateTime nowUtc, TimeSpan leaseDuration, CancellationToken cancellationToken = default)
    {
        var builder = Builders<ActivityLogIngestQueueItem>.Filter;
        var filter = builder.Lte(item => item.AvailableAtUtc, nowUtc)
            & builder.Or(
                builder.Eq(item => item.Status, ActivityLogIngestQueueStatuses.Pending),
                builder.And(
                    builder.Eq(item => item.Status, ActivityLogIngestQueueStatuses.Processing),
                    builder.Lte(item => item.LeaseExpiresAtUtc, nowUtc)));

        var update = Builders<ActivityLogIngestQueueItem>.Update
            .Set(item => item.Status, ActivityLogIngestQueueStatuses.Processing)
            .Set(item => item.LeaseOwner, workerId)
            .Set(item => item.LeaseExpiresAtUtc, nowUtc.Add(leaseDuration))
            .Set(item => item.UpdatedAtUtc, nowUtc)
            .Inc(item => item.AttemptCount, 1);

        var options = new FindOneAndUpdateOptions<ActivityLogIngestQueueItem>
        {
            Sort = Builders<ActivityLogIngestQueueItem>.Sort.Ascending(item => item.AvailableAtUtc).Ascending(item => item.ReceivedAtUtc),
            ReturnDocument = ReturnDocument.After
        };

        return await _context.ActivityLogIngestQueue.FindOneAndUpdateAsync(filter, update, options, cancellationToken);
    }

    public Task MarkCompletedAsync(string id, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var update = Builders<ActivityLogIngestQueueItem>.Update
            .Set(item => item.Status, ActivityLogIngestQueueStatuses.Completed)
            .Set(item => item.ProcessedAtUtc, nowUtc)
            .Set(item => item.UpdatedAtUtc, nowUtc)
            .Set(item => item.LastError, string.Empty)
            .Unset(item => item.LeaseOwner)
            .Unset(item => item.LeaseExpiresAtUtc);

        return _context.ActivityLogIngestQueue.UpdateOneAsync(item => item.Id == id, update, cancellationToken: cancellationToken);
    }

    public Task MarkPendingAsync(string id, string error, TimeSpan retryDelay, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var update = Builders<ActivityLogIngestQueueItem>.Update
            .Set(item => item.Status, ActivityLogIngestQueueStatuses.Pending)
            .Set(item => item.AvailableAtUtc, nowUtc.Add(retryDelay))
            .Set(item => item.UpdatedAtUtc, nowUtc)
            .Set(item => item.LastError, error)
            .Unset(item => item.LeaseOwner)
            .Unset(item => item.LeaseExpiresAtUtc);

        return _context.ActivityLogIngestQueue.UpdateOneAsync(item => item.Id == id, update, cancellationToken: cancellationToken);
    }

    public Task MarkFailedAsync(string id, string error, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var update = Builders<ActivityLogIngestQueueItem>.Update
            .Set(item => item.Status, ActivityLogIngestQueueStatuses.Failed)
            .Set(item => item.UpdatedAtUtc, nowUtc)
            .Set(item => item.LastError, error)
            .Unset(item => item.LeaseOwner)
            .Unset(item => item.LeaseExpiresAtUtc);

        return _context.ActivityLogIngestQueue.UpdateOneAsync(item => item.Id == id, update, cancellationToken: cancellationToken);
    }

    public async Task<bool> RetryFailedAsync(string id, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var filter = Builders<ActivityLogIngestQueueItem>.Filter.Eq(item => item.Id, id)
            & Builders<ActivityLogIngestQueueItem>.Filter.Eq(item => item.Status, ActivityLogIngestQueueStatuses.Failed);

        var update = Builders<ActivityLogIngestQueueItem>.Update
            .Set(item => item.Status, ActivityLogIngestQueueStatuses.Pending)
            .Set(item => item.AvailableAtUtc, nowUtc)
            .Set(item => item.UpdatedAtUtc, nowUtc)
            .Set(item => item.AttemptCount, 0)
            .Set(item => item.LastError, string.Empty)
            .Unset(item => item.ProcessedAtUtc)
            .Unset(item => item.LeaseOwner)
            .Unset(item => item.LeaseExpiresAtUtc);

        var result = await _context.ActivityLogIngestQueue.UpdateOneAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount > 0;
    }

    public async Task<long> RetryFailedAsync(ActivityLogIngestQueueQuery query, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var retryQuery = new ActivityLogIngestQueueQuery
        {
            PartnerId = query.PartnerId,
            Status = ActivityLogIngestQueueStatuses.Failed,
            FromUtc = query.FromUtc,
            ToUtc = query.ToUtc,
            Page = query.Page,
            PageSize = query.PageSize
        };

        var filter = BuildFilter(retryQuery);
        var update = Builders<ActivityLogIngestQueueItem>.Update
            .Set(item => item.Status, ActivityLogIngestQueueStatuses.Pending)
            .Set(item => item.AvailableAtUtc, nowUtc)
            .Set(item => item.UpdatedAtUtc, nowUtc)
            .Set(item => item.AttemptCount, 0)
            .Set(item => item.LastError, string.Empty)
            .Unset(item => item.ProcessedAtUtc)
            .Unset(item => item.LeaseOwner)
            .Unset(item => item.LeaseExpiresAtUtc);

        var result = await _context.ActivityLogIngestQueue.UpdateManyAsync(filter, update, cancellationToken: cancellationToken);
        return result.ModifiedCount;
    }

    private static FilterDefinition<ActivityLogIngestQueueItem> BuildFilter(ActivityLogIngestQueueQuery query)
    {
        var builder = Builders<ActivityLogIngestQueueItem>.Filter;
        var filters = new List<FilterDefinition<ActivityLogIngestQueueItem>>();

        if (!string.IsNullOrWhiteSpace(query.PartnerId))
        {
            filters.Add(builder.Eq(item => item.PartnerId, query.PartnerId.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            filters.Add(builder.Eq(item => item.Status, query.Status.Trim()));
        }

        if (query.FromUtc.HasValue)
        {
            filters.Add(builder.Gte(item => item.ReceivedAtUtc, query.FromUtc.Value));
        }

        if (query.ToUtc.HasValue)
        {
            filters.Add(builder.Lte(item => item.ReceivedAtUtc, query.ToUtc.Value));
        }

        return filters.Count == 0 ? builder.Empty : builder.And(filters);
    }
}