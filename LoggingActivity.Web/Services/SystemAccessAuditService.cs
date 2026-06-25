using System.Security.Claims;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;

namespace LoggingActivity.Web.Services;

public sealed class SystemAccessAuditService
{
    public const string SessionClaimType = "session_id";

    private readonly ISystemAccessLogRepository _systemAccessLogRepository;
    private readonly IUserActiveSessionRepository _userActiveSessionRepository;

    public SystemAccessAuditService(
        ISystemAccessLogRepository systemAccessLogRepository,
        IUserActiveSessionRepository userActiveSessionRepository)
    {
        _systemAccessLogRepository = systemAccessLogRepository;
        _userActiveSessionRepository = userActiveSessionRepository;
    }

    public Task<PagedResult<SystemAccessLog>> GetPagedAsync(SystemAccessLogQuery query, CancellationToken cancellationToken = default)
    {
        return _systemAccessLogRepository.GetPagedAsync(query, cancellationToken);
    }

    public async Task<bool> ActivateSessionAsync(HttpContext httpContext, AppUser user, string sessionId, CancellationToken cancellationToken = default)
    {
        var safeUserName = NormalizeUserName(user.UserName);
        var normalizedUserName = NormalizeUserNameKey(safeUserName);
        var existing = await _userActiveSessionRepository.GetByNormalizedUserNameAsync(normalizedUserName, cancellationToken);
        var replacedExistingSession = existing is not null && !string.Equals(existing.SessionId, sessionId, StringComparison.Ordinal);

        await _userActiveSessionRepository.UpsertAsync(new UserActiveSession
        {
            NormalizedUserName = normalizedUserName,
            UserName = safeUserName,
            UserId = string.IsNullOrWhiteSpace(user.Id) ? safeUserName : user.Id,
            DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? safeUserName : user.DisplayName.Trim(),
            Role = string.IsNullOrWhiteSpace(user.Role) ? SystemRoles.Auditor : user.Role.Trim(),
            SessionId = sessionId,
            IpAddress = GetIpAddress(httpContext),
            UserAgent = GetUserAgent(httpContext),
            IssuedAtUtc = DateTime.UtcNow,
            LastSeenAtUtc = DateTime.UtcNow
        }, cancellationToken);

        return replacedExistingSession;
    }

    public Task<bool> IsSessionActiveAsync(string? userName, string? sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(sessionId))
        {
            return Task.FromResult(false);
        }

        return _userActiveSessionRepository.IsSessionActiveAsync(
            NormalizeUserNameKey(userName),
            sessionId.Trim(),
            cancellationToken);
    }

    public Task TouchSessionAsync(string? userName, string? sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(sessionId))
        {
            return Task.CompletedTask;
        }

        return _userActiveSessionRepository.TouchAsync(
            NormalizeUserNameKey(userName),
            sessionId.Trim(),
            DateTime.UtcNow,
            cancellationToken);
    }

    public Task DeactivateSessionAsync(string? userName, string? sessionId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userName) || string.IsNullOrWhiteSpace(sessionId))
        {
            return Task.CompletedTask;
        }

        return _userActiveSessionRepository.RemoveIfMatchesAsync(
            NormalizeUserNameKey(userName),
            sessionId.Trim(),
            cancellationToken);
    }

    public Task RecordLoginAsync(HttpContext httpContext, AppUser user, string sessionId, bool replacedExistingSession, CancellationToken cancellationToken = default)
    {
        var safeUserName = NormalizeUserName(user.UserName);
        return _systemAccessLogRepository.AddAsync(new SystemAccessLog
        {
            UserId = string.IsNullOrWhiteSpace(user.Id) ? safeUserName : user.Id,
            UserName = safeUserName,
            DisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? safeUserName : user.DisplayName.Trim(),
            Role = string.IsNullOrWhiteSpace(user.Role) ? SystemRoles.Auditor : user.Role.Trim(),
            SessionId = sessionId,
            EventType = replacedExistingSession ? SystemAccessEventTypes.SessionReplaced : SystemAccessEventTypes.Login,
            Description = replacedExistingSession
                ? "Đăng nhập thành công và thay thế phiên đăng nhập trước đó của cùng tài khoản."
                : "Đăng nhập thành công.",
            Endpoint = httpContext.Request.Path,
            HttpMethod = httpContext.Request.Method,
            IpAddress = GetIpAddress(httpContext),
            UserAgent = GetUserAgent(httpContext),
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);
    }

    public Task RecordLogoutAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        return _systemAccessLogRepository.AddAsync(BuildCurrentUserLog(httpContext, SystemAccessEventTypes.Logout, "Đăng xuất thành công."), cancellationToken);
    }

    public Task RecordSecurityActionAsync(HttpContext httpContext, string eventType, string description, CancellationToken cancellationToken = default)
    {
        var log = BuildCurrentUserLog(httpContext, eventType, description);
        return _systemAccessLogRepository.AddAsync(log, cancellationToken);
    }

    public Task RecordSessionRejectedAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
    {
        var log = BuildCurrentUserLog(
            httpContext,
            SystemAccessEventTypes.SessionRejected,
            "Phiên đăng nhập hết hiệu lực do tài khoản đăng nhập ở nơi khác.");
        return _systemAccessLogRepository.AddAsync(log, cancellationToken);
    }

    private static SystemAccessLog BuildCurrentUserLog(HttpContext httpContext, string eventType, string description)
    {
        var user = httpContext.User;
        var userName = NormalizeUserName(user.Identity?.Name);
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var displayName = user.FindFirst(ClaimTypes.GivenName)?.Value;
        var role = user.FindFirst(ClaimTypes.Role)?.Value;
        var sessionId = user.FindFirst(SessionClaimType)?.Value;

        return new SystemAccessLog
        {
            UserId = string.IsNullOrWhiteSpace(userId) ? userName : userId.Trim(),
            UserName = userName,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? userName : displayName.Trim(),
            Role = string.IsNullOrWhiteSpace(role) ? SystemRoles.Auditor : role.Trim(),
            SessionId = string.IsNullOrWhiteSpace(sessionId) ? string.Empty : sessionId.Trim(),
            EventType = eventType,
            Description = description,
            Endpoint = httpContext.Request.Path,
            HttpMethod = httpContext.Request.Method,
            IpAddress = GetIpAddress(httpContext),
            UserAgent = GetUserAgent(httpContext),
            CreatedAtUtc = DateTime.UtcNow
        };
    }

    private static string NormalizeUserName(string? userName)
    {
        return string.IsNullOrWhiteSpace(userName) ? "unknown-user" : userName.Trim();
    }

    private static string NormalizeUserNameKey(string? userName)
    {
        return NormalizeUserName(userName).ToUpperInvariant();
    }

    private static string GetIpAddress(HttpContext httpContext)
    {
        return httpContext.Connection.RemoteIpAddress?.ToString() ?? string.Empty;
    }

    private static string GetUserAgent(HttpContext httpContext)
    {
        return httpContext.Request.Headers.UserAgent.ToString();
    }
}
