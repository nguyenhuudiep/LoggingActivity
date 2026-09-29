using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Driver;

namespace LoggingActivity.Web.Services;

public sealed class SingleSessionCookieEvents : CookieAuthenticationEvents
{
    // Session đã xác thực hợp lệ được nhớ trong thời gian ngắn để không phải đọc + ghi MongoDB ở mọi request.
    private static readonly TimeSpan ValidatedSessionCacheDuration = TimeSpan.FromSeconds(30);

    private readonly SystemAccessAuditService _systemAccessAuditService;
    private readonly IMemoryCache _cache;
    private readonly ILogger<SingleSessionCookieEvents> _logger;

    public SingleSessionCookieEvents(
        SystemAccessAuditService systemAccessAuditService,
        IMemoryCache cache,
        ILogger<SingleSessionCookieEvents> logger)
    {
        _systemAccessAuditService = systemAccessAuditService;
        _cache = cache;
        _logger = logger;
    }

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        if (principal?.Identity?.IsAuthenticated != true)
        {
            return;
        }

        var userName = principal.Identity?.Name;
        var sessionId = principal.FindFirst(SystemAccessAuditService.SessionClaimType)?.Value;
        var cancellationToken = context.HttpContext.RequestAborted;
        var cacheKey = $"session-validated:{userName}:{sessionId}";
        if (_cache.TryGetValue(cacheKey, out _))
        {
            return;
        }

        bool isActive;
        try
        {
            isActive = await _systemAccessAuditService.IsSessionActiveAsync(userName, sessionId, cancellationToken);
        }
        catch (Exception ex) when (IsMongoUnavailable(ex))
        {
            _logger.LogWarning(ex, "MongoDB unavailable while validating session for user {UserName}. Skip single-session enforcement.", userName);
            return;
        }

        if (isActive)
        {
            try
            {
                await _systemAccessAuditService.TouchSessionAsync(userName, sessionId, cancellationToken);
            }
            catch (Exception ex) when (IsMongoUnavailable(ex))
            {
                _logger.LogWarning(ex, "MongoDB unavailable while touching session for user {UserName}.", userName);
            }

            _cache.Set(cacheKey, true, ValidatedSessionCacheDuration);
            return;
        }

        try
        {
            await _systemAccessAuditService.RecordSessionRejectedAsync(context.HttpContext, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Khong the ghi nhat ky session rejected cho user {UserName}", userName);
        }

        context.RejectPrincipal();
        await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static bool IsMongoUnavailable(Exception exception)
    {
        if (exception is TimeoutException or MongoConnectionException or MongoException)
        {
            return true;
        }

        return exception.InnerException is not null && IsMongoUnavailable(exception.InnerException);
    }
}
