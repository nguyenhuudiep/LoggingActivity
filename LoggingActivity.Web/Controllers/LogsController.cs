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

        var query = new LogQuery
        {
            SearchTerm = filter.SearchTerm,
            PartnerId = filter.PartnerId,
            Action = filter.Action,
            FromUtc = filter.From,
            ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        var logs = await _activityLogService.GetPagedAsync(query, cancellationToken);
        var statistics = await _activityLogService.GetStatisticsAsync(query, cancellationToken);
        var activeWarnings = await _alertRuleService.GetActiveWarningsAsync(cancellationToken);
        var unconfiguredActionWarnings = await _alertRuleService.GetUnconfiguredActionWarningsAsync(cancellationToken);
        var availableActions = await _logActionDefinitionService.GetActiveAsync(cancellationToken);
        var availablePartners = await _partnerService.GetAllAsync(cancellationToken);

        return View(new LogDashboardViewModel
        {
            Filter = filter,
            Logs = logs,
            Statistics = statistics,
            AvailableActions = availableActions,
            AvailablePartners = availablePartners,
            ActiveWarnings = activeWarnings,
            UnconfiguredActionWarnings = unconfiguredActionWarnings
        });
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
}