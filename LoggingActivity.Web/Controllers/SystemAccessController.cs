using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin + "," + SystemRoles.Auditor)]
public sealed class SystemAccessController : AppController
{
    private readonly SystemAccessAuditService _systemAccessAuditService;

    public SystemAccessController(SystemAccessAuditService systemAccessAuditService)
    {
        _systemAccessAuditService = systemAccessAuditService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] SystemAccessLogFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.SystemAccessHistory, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        filter.From ??= DateTime.Today.AddDays(-6);
        filter.To ??= DateTime.Today;

        var logs = await _systemAccessAuditService.GetPagedAsync(new SystemAccessLogQuery
        {
            SearchTerm = filter.SearchTerm,
            EventType = filter.EventType,
            FromUtc = filter.From,
            ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
            Page = filter.Page,
            PageSize = filter.PageSize
        }, cancellationToken);

        return View(new SystemAccessLogListViewModel
        {
            Filter = filter,
            Logs = logs
        });
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] SystemAccessLogFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.SystemAccessHistory, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        filter.From ??= DateTime.Today.AddDays(-6);
        filter.To ??= DateTime.Today;

        var logs = await ReadAllPagesAsync(
            (page, pageSize, token) => _systemAccessAuditService.GetPagedAsync(new SystemAccessLogQuery
            {
                SearchTerm = filter.SearchTerm,
                EventType = filter.EventType,
                FromUtc = filter.From,
                ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
                Page = page,
                PageSize = pageSize
            }, token),
            cancellationToken);

        var rows = logs.Select(log => (IReadOnlyList<string?>)
        [
            log.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
            log.UserName,
            log.DisplayName,
            log.Role,
            log.EventType,
            log.Description,
            log.HttpMethod,
            log.Endpoint,
            log.IpAddress,
            log.UserAgent,
            log.SessionId
        ]).ToList();

        return BuildCsvFile(
            "system-access-history",
            ["CreatedAtUtc", "UserName", "DisplayName", "Role", "EventType", "Description", "HttpMethod", "Endpoint", "IpAddress", "UserAgent", "SessionId"],
            rows);
    }
}
