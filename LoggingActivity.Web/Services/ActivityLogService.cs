using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;
using Microsoft.Extensions.Logging;

namespace LoggingActivity.Web.Services;

public sealed class ActivityLogService
{
    private readonly IActivityLogRepository _activityLogRepository;
    private readonly AlertRuleService _alertRuleService;
    private readonly ILogger<ActivityLogService> _logger;

    public ActivityLogService(
        IActivityLogRepository activityLogRepository,
        AlertRuleService alertRuleService,
        ILogger<ActivityLogService> logger)
    {
        _activityLogRepository = activityLogRepository;
        _alertRuleService = alertRuleService;
        _logger = logger;
    }

    public async Task AddAsync(ActivityLog logEntry, CancellationToken cancellationToken = default)
    {
        await _activityLogRepository.AddAsync(logEntry, cancellationToken);

        try
        {
            await _alertRuleService.RecordTriggeredAlertAsync(logEntry, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể xử lý cảnh báo hậu kỳ cho log {Action} của user {UserId}.", logEntry.Action, logEntry.ExternalUserId);
        }
    }

    public Task<PagedResult<ActivityLog>> GetPagedAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        return _activityLogRepository.GetPagedAsync(query, cancellationToken);
    }

    public Task<PagedResult<ActivityLog>> GetPagedByUserAsync(string userId, LogQuery query, CancellationToken cancellationToken = default)
    {
        return _activityLogRepository.GetPagedByUserAsync(userId, query, cancellationToken);
    }

    public Task<LogStatistics> GetStatisticsAsync(LogQuery query, CancellationToken cancellationToken = default)
    {
        return _activityLogRepository.GetStatisticsAsync(query, cancellationToken);
    }

    public Task<LogStatistics> GetStatisticsByUserAsync(string userId, LogQuery query, CancellationToken cancellationToken = default)
    {
        return _activityLogRepository.GetStatisticsByUserAsync(userId, query, cancellationToken);
    }

    public Task LogAsync(
        string action,
        string description,
        HttpContext httpContext,
        string source,
        CancellationToken cancellationToken = default)
    {
        var userId = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
        var userName = httpContext.User.Identity?.Name ?? "Anonymous";
        var role = httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.Role)?.Value ?? string.Empty;

        return _activityLogRepository.AddAsync(new ActivityLog
        {
            UserId = userId,
            UserName = userName,
            Role = role,
            Action = action,
            Description = description,
            Endpoint = httpContext.Request.Path,
            Source = source,
            HttpMethod = httpContext.Request.Method,
            IpAddress = httpContext.Connection.RemoteIpAddress?.ToString(),
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }
}