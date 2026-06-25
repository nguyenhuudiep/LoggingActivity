using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LoggingActivity.Web.Services;

public sealed class SingleSessionCookieEvents : CookieAuthenticationEvents
{
    private readonly SystemAccessAuditService _systemAccessAuditService;
    private readonly ILogger<SingleSessionCookieEvents> _logger;

    public SingleSessionCookieEvents(
        SystemAccessAuditService systemAccessAuditService,
        ILogger<SingleSessionCookieEvents> logger)
    {
        _systemAccessAuditService = systemAccessAuditService;
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

        var isActive = await _systemAccessAuditService.IsSessionActiveAsync(userName, sessionId, cancellationToken);
        if (isActive)
        {
            await _systemAccessAuditService.TouchSessionAsync(userName, sessionId, cancellationToken);
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
}
