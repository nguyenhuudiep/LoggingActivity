using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;

namespace LoggingActivity.Web.Services;

public sealed class ActivityLogService
{
    private readonly IActivityLogRepository _activityLogRepository;
    private readonly AlertRuleService _alertRuleService;

    public ActivityLogService(IActivityLogRepository activityLogRepository, AlertRuleService alertRuleService)
    {
        _activityLogRepository = activityLogRepository;
        _alertRuleService = alertRuleService;
    }

    public async Task AddAsync(ActivityLog logEntry, CancellationToken cancellationToken = default)
    {
        await _activityLogRepository.AddAsync(logEntry, cancellationToken);
        await _alertRuleService.RecordTriggeredAlertAsync(logEntry, cancellationToken);
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