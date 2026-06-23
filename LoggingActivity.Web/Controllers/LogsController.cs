using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin + "," + SystemRoles.Auditor)]
public sealed class LogsController : AppController
{
    private readonly ActivityLogService _activityLogService;
    private readonly AlertRuleService _alertRuleService;
    private readonly LogActionDefinitionService _logActionDefinitionService;
    private readonly PartnerService _partnerService;

    public LogsController(
        ActivityLogService activityLogService,
        AlertRuleService alertRuleService,
        LogActionDefinitionService logActionDefinitionService,
        PartnerService partnerService)
    {
        _activityLogService = activityLogService;
        _alertRuleService = alertRuleService;
        _logActionDefinitionService = logActionDefinitionService;
        _partnerService = partnerService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] LogFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogDashboard, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        filter.From ??= DateTime.Today;
        filter.To ??= DateTime.Today;

        var query = BuildLogQuery(filter);

        var logs = await _activityLogService.GetPagedAsync(query, cancellationToken);
        var availableActions = await _logActionDefinitionService.GetActiveAsync(cancellationToken);
        var availablePartners = await _partnerService.GetAllAsync(cancellationToken);

        return View(new LogDashboardViewModel
        {
            Filter = filter,
            Logs = logs,
            AvailableActions = availableActions,
            AvailablePartners = availablePartners
        });
    }

    [HttpGet]
    public async Task<IActionResult> Insights([FromQuery] LogFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogDashboard, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        filter.From ??= DateTime.Today;
        filter.To ??= DateTime.Today;
        var query = BuildLogQuery(filter);

        var statisticsTask = _activityLogService.GetStatisticsAsync(query, cancellationToken);
        var activeWarningsTask = _alertRuleService.GetActiveWarningsAsync(cancellationToken);
        var unconfiguredActionWarningsTask = _alertRuleService.GetUnconfiguredActionWarningsAsync(cancellationToken);

        await Task.WhenAll(statisticsTask, activeWarningsTask, unconfiguredActionWarningsTask);

        return PartialView("_Insights", new LogInsightsViewModel
        {
            Statistics = statisticsTask.Result,
            ActiveWarnings = activeWarningsTask.Result,
            UnconfiguredActionWarnings = unconfiguredActionWarningsTask.Result
        });
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] LogFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogDashboard, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        filter.From ??= DateTime.Today;
        filter.To ??= DateTime.Today;

        var logs = await ReadAllPagesAsync(
            (page, pageSize, token) => _activityLogService.GetPagedAsync(new LogQuery
            {
                SearchTerm = filter.SearchTerm,
                PartnerId = filter.PartnerId,
                Action = filter.Action,
                FromUtc = filter.From,
                ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
                Page = page,
                PageSize = pageSize
            }, token),
            cancellationToken);

        var rows = logs.Select(log => (IReadOnlyList<string?>)
        [
            log.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
            log.PartnerName,
            log.DisplayActorIdentifier,
            log.DisplayActorIdentifierType,
            log.UserName,
            log.Role,
            log.Action,
            log.Description,
            log.Endpoint,
            log.Source,
            log.HttpMethod,
            log.IpAddress
        ]).ToList();

        return BuildCsvFile(
            "activity-logs",
            ["CreatedAtUtc", "PartnerName", "ActorIdentifier", "ActorIdentifierType", "UserName", "Role", "Action", "Description", "Endpoint", "Source", "HttpMethod", "IpAddress"],
            rows);
    }

    [HttpGet]
    public async Task<IActionResult> ActorDetails([FromQuery] ActorLogDetailsFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogDashboard, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var actorIdentifier = ActorIdentityHelper.NormalizeIdentifier(filter.ActorIdentifier);
        if (string.IsNullOrWhiteSpace(actorIdentifier))
        {
            return BadRequest("Thiếu key cần tra cứu.");
        }

        var actorIdentifierType = ActorIdentityHelper.NormalizeType(filter.ActorIdentifierType, actorIdentifier);
        var query = new LogQuery
        {
            PartnerId = filter.PartnerId,
            Action = filter.Action,
            ActorIdentifier = actorIdentifier,
            ActorIdentifierType = actorIdentifierType,
            FromUtc = filter.From?.Date,
            ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        return PartialView("_ActorLogDetailsModalContent", new ActorLogDetailsViewModel
        {
            Filter = filter,
            Logs = await _activityLogService.GetPagedAsync(query, cancellationToken),
            ActorIdentifier = actorIdentifier,
            ActorIdentifierType = actorIdentifierType,
            ActorLabel = ActorIdentityHelper.BuildDisplayLabel(actorIdentifierType)
        });
    }

    private static LogQuery BuildLogQuery(LogFilterViewModel filter)
    {
        return new LogQuery
        {
            SearchTerm = filter.SearchTerm,
            PartnerId = filter.PartnerId,
            Action = filter.Action,
            FromUtc = filter.From,
            ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
            Page = filter.Page,
            PageSize = filter.PageSize
        };
    }
}