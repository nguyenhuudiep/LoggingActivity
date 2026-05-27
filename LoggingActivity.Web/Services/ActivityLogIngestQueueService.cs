using LoggingActivity.Web.Contracts;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;
using Microsoft.Extensions.Logging;

namespace LoggingActivity.Web.Services;

public sealed class ActivityLogIngestQueueService
{
    private static readonly TimeSpan LeaseDuration = TimeSpan.FromMinutes(1);

    private readonly IActivityLogIngestQueueRepository _queueRepository;
    private readonly ActivityLogService _activityLogService;
    private readonly LogActionDefinitionService _logActionDefinitionService;
    private readonly ILogger<ActivityLogIngestQueueService> _logger;

    public ActivityLogIngestQueueService(
        IActivityLogIngestQueueRepository queueRepository,
        ActivityLogService activityLogService,
        LogActionDefinitionService logActionDefinitionService,
        ILogger<ActivityLogIngestQueueService> logger)
    {
        _queueRepository = queueRepository;
        _activityLogService = activityLogService;
        _logActionDefinitionService = logActionDefinitionService;
        _logger = logger;
    }

    public Task EnsureInfrastructureAsync(CancellationToken cancellationToken = default)
    {
        return _queueRepository.EnsureIndexesAsync(cancellationToken);
    }

    public Task<PagedResult<ActivityLogIngestQueueItem>> GetPagedAsync(ActivityLogIngestQueueQuery query, CancellationToken cancellationToken = default)
    {
        return _queueRepository.GetPagedAsync(query, cancellationToken);
    }

    public Task<ActivityLogIngestQueueSummary> GetSummaryAsync(ActivityLogIngestQueueQuery query, CancellationToken cancellationToken = default)
    {
        return _queueRepository.GetSummaryAsync(query, cancellationToken);
    }

    public Task<long> DeleteDemoStatesAsync(CancellationToken cancellationToken = default)
    {
        return _queueRepository.DeleteDemoAsync(cancellationToken);
    }

    public Task<bool> RetryFailedAsync(string id, CancellationToken cancellationToken = default)
    {
        return _queueRepository.RetryFailedAsync(id, cancellationToken);
    }

    public Task<long> RetryFailedAsync(ActivityLogIngestQueueQuery query, CancellationToken cancellationToken = default)
    {
        return _queueRepository.RetryFailedAsync(query, cancellationToken);
    }

    public async Task<int> SeedDemoStatesAsync(Partner? partner, CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var partnerName = string.IsNullOrWhiteSpace(partner?.Name) ? "Demo Partner" : partner!.Name.Trim();
        var partnerId = partner?.Id;
        var batchId = nowUtc.ToString("yyyyMMddHHmmss");

        await _queueRepository.EnqueueAsync(new ActivityLogIngestQueueItem
        {
            DeduplicationKey = $"demo-pending:{batchId}",
            RequestId = $"demo-pending-{batchId}",
            PartnerId = partnerId,
            PartnerName = partnerName,
            ExternalUserId = 900001,
            UserName = "demo_pending_user",
            Action = "DEMO_PENDING_RETRY",
            Description = "Bản ghi demo cho trang queue ở trạng thái chờ retry.",
            Endpoint = "/demo/ingest/pending-retry",
            Source = ActivityLogSources.IntegratedApi,
            HttpMethod = "POST",
            IpAddress = "127.0.0.1",
            Status = ActivityLogIngestQueueStatuses.Pending,
            AttemptCount = 2,
            LastError = "Timeout khi gọi xử lý xuống database ở lần thử trước.",
            ReceivedAtUtc = nowUtc.AddMinutes(-8),
            AvailableAtUtc = nowUtc.AddMinutes(4),
            UpdatedAtUtc = nowUtc
        }, cancellationToken);

        await _queueRepository.EnqueueAsync(new ActivityLogIngestQueueItem
        {
            DeduplicationKey = $"demo-failed:{batchId}",
            RequestId = $"demo-failed-{batchId}",
            PartnerId = partnerId,
            PartnerName = partnerName,
            ExternalUserId = 900002,
            UserName = "demo_failed_user",
            Action = "DEMO_FAILED_FINAL",
            Description = "Bản ghi demo cho trang queue ở trạng thái thất bại.",
            Endpoint = "/demo/ingest/failed-final",
            Source = ActivityLogSources.IntegratedApi,
            HttpMethod = "POST",
            IpAddress = "127.0.0.1",
            Status = ActivityLogIngestQueueStatuses.Failed,
            AttemptCount = 5,
            LastError = "Action DEMO_FAILED_FINAL đang bị khóa trong cấu hình hoặc gặp lỗi nghiêm trọng.",
            ReceivedAtUtc = nowUtc.AddMinutes(-15),
            AvailableAtUtc = nowUtc.AddMinutes(-10),
            ProcessedAtUtc = nowUtc.AddMinutes(-3),
            UpdatedAtUtc = nowUtc.AddMinutes(-3)
        }, cancellationToken);

        return 2;
    }

    public async Task<(string RequestId, bool Accepted)> EnqueueAsync(
        Partner partner,
        PartnerActivityRequest request,
        HttpContext httpContext,
        CancellationToken cancellationToken = default)
    {
        var requestId = string.IsNullOrWhiteSpace(request.RequestId)
            ? Guid.NewGuid().ToString("N")
            : request.RequestId.Trim();

        var accepted = await _queueRepository.EnqueueAsync(new ActivityLogIngestQueueItem
        {
            DeduplicationKey = $"{partner.Id}:{requestId}",
            RequestId = requestId,
            PartnerId = partner.Id,
            PartnerName = string.IsNullOrWhiteSpace(partner.Name) ? "N/A" : partner.Name.Trim(),
            ExternalUserId = request.UserId,
            UserName = string.IsNullOrWhiteSpace(request.UserName) ? "Anonymous" : request.UserName.Trim(),
            Action = request.Action.Trim(),
            Description = request.Description.Trim(),
            Endpoint = string.IsNullOrWhiteSpace(request.Endpoint) ? httpContext.Request.Path : request.Endpoint.Trim(),
            Source = ActivityLogSources.IntegratedApi,
            HttpMethod = httpContext.Request.Method,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            ReceivedAtUtc = DateTime.UtcNow,
            AvailableAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        return (requestId, accepted);
    }

    public async Task<bool> ProcessNextAsync(string workerId, CancellationToken cancellationToken = default)
    {
        var item = await _queueRepository.TryLeaseNextAsync(workerId, DateTime.UtcNow, LeaseDuration, cancellationToken);
        if (item is null)
        {
            return false;
        }

        try
        {
            var ensuredAction = await _logActionDefinitionService.EnsureApiActionReadyAsync(item.Action, cancellationToken);
            if (!ensuredAction.Success)
            {
                await _queueRepository.MarkFailedAsync(item.Id!, ensuredAction.Error ?? "Không thể xử lý action log.", cancellationToken);
                return true;
            }

            await _activityLogService.AddAsync(new ActivityLog
            {
                PartnerId = item.PartnerId,
                PartnerName = item.PartnerName,
                UserId = item.PartnerId,
                ExternalUserId = item.ExternalUserId,
                UserName = item.UserName,
                Role = "Partner",
                Action = ensuredAction.NormalizedCode!,
                Description = item.Description,
                Endpoint = item.Endpoint,
                Source = item.Source,
                HttpMethod = item.HttpMethod,
                IpAddress = item.IpAddress,
                CreatedAtUtc = item.ReceivedAtUtc
            }, cancellationToken);

            await _queueRepository.MarkCompletedAsync(item.Id!, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể xử lý hàng đợi ingest log cho requestId {RequestId}.", item.RequestId);

            if (item.AttemptCount >= 5)
            {
                await _queueRepository.MarkFailedAsync(item.Id!, ex.Message, cancellationToken);
                return true;
            }

            var retryDelay = TimeSpan.FromSeconds(Math.Min(item.AttemptCount * 5, 60));
            await _queueRepository.MarkPendingAsync(item.Id!, ex.Message, retryDelay, cancellationToken);
        }

        return true;
    }
}