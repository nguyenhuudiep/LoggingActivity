using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin + "," + SystemRoles.Auditor)]
public sealed class AlertHistoryController : AppController
{
    private readonly AlertHistoryService _alertHistoryService;
    private readonly AlertRuleService _alertRuleService;
    private readonly PartnerService _partnerService;
    private readonly LogActionDefinitionService _logActionDefinitionService;

    public AlertHistoryController(
        AlertHistoryService alertHistoryService,
        AlertRuleService alertRuleService,
        PartnerService partnerService,
        LogActionDefinitionService logActionDefinitionService)
    {
        _alertHistoryService = alertHistoryService;
        _alertRuleService = alertRuleService;
        _partnerService = partnerService;
        _logActionDefinitionService = logActionDefinitionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AlertHistoryFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.AlertHistory, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        filter.From ??= DateTime.Today.AddDays(-6);
        filter.To ??= DateTime.Today;

        var query = new AlertHistoryQuery
        {
            SearchTerm = filter.SearchTerm,
            PartnerId = filter.PartnerId,
            Action = filter.Action,
            Status = filter.Status,
            FromUtc = filter.From?.Date,
            ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        if (query.FromUtc.HasValue && query.ToUtc.HasValue)
        {
            await _alertRuleService.EnsureAlertHistoryBackfillAsync(query.FromUtc.Value, query.ToUtc.Value.AddTicks(1), cancellationToken);
        }

        return View(new AlertHistoryListViewModel
        {
            Filter = filter,
            Alerts = await _alertHistoryService.GetPagedAsync(query, cancellationToken),
            AvailablePartners = await _partnerService.GetAllAsync(cancellationToken),
            AvailableActions = await _logActionDefinitionService.GetActiveAsync(cancellationToken)
        });
    }
}