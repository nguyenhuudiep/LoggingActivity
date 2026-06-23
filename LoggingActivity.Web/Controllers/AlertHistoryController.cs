using LoggingActivity.Web.Models;
using LoggingActivity.Web.Infrastructure;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin + "," + SystemRoles.Auditor)]
public sealed class AlertHistoryController : AppController
{
    private readonly AlertHistoryService _alertHistoryService;
    private readonly PartnerService _partnerService;
    private readonly LogActionDefinitionService _logActionDefinitionService;
    private readonly ActivityLogService _activityLogService;

    public AlertHistoryController(
        AlertHistoryService alertHistoryService,
        PartnerService partnerService,
        LogActionDefinitionService logActionDefinitionService,
        ActivityLogService activityLogService)
    {
        _alertHistoryService = alertHistoryService;
        _partnerService = partnerService;
        _logActionDefinitionService = logActionDefinitionService;
        _activityLogService = activityLogService;
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

        var alertsTask = _alertHistoryService.GetPagedAsync(query, cancellationToken);
        var availablePartnersTask = _partnerService.GetAllAsync(cancellationToken);
        var availableActionsTask = _logActionDefinitionService.GetActiveAsync(cancellationToken);

        await Task.WhenAll(alertsTask, availablePartnersTask, availableActionsTask);

        return View(new AlertHistoryListViewModel
        {
            Filter = filter,
            Alerts = alertsTask.Result,
            AvailablePartners = availablePartnersTask.Result,
            AvailableActions = availableActionsTask.Result
        });
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] AlertHistoryFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.AlertHistory, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        filter.From ??= DateTime.Today.AddDays(-6);
        filter.To ??= DateTime.Today;

        var alerts = await ReadAllPagesAsync(
            (page, pageSize, token) => _alertHistoryService.GetPagedAsync(new AlertHistoryQuery
            {
                SearchTerm = filter.SearchTerm,
                PartnerId = filter.PartnerId,
                Action = filter.Action,
                Status = filter.Status,
                FromUtc = filter.From?.Date,
                ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
                Page = page,
                PageSize = pageSize
            }, token),
            cancellationToken);

        var rows = alerts.Select(alert => (IReadOnlyList<string?>)
        [
            alert.OccurredAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
            alert.AlertDateUtc.ToString("yyyy-MM-dd"),
            alert.PartnerName,
            alert.DisplayActorIdentifier,
            alert.UserName,
            alert.Action,
            alert.DailyLimit.ToString(),
            alert.CurrentCount.ToString(),
            alert.Message
        ]).ToList();

        return BuildCsvFile(
            "alert-history",
            ["OccurredAtUtc", "AlertDateUtc", "PartnerName", "ActorIdentifier", "UserName", "Action", "DailyLimit", "CurrentCount", "Message"],
            rows);
    }

    [HttpGet]
    public async Task<IActionResult> Details([FromQuery] AlertHistoryDetailsFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.AlertHistory, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var actorIdentifier = ActorIdentityHelper.NormalizeIdentifier(filter.ActorIdentifier);
        if (string.IsNullOrWhiteSpace(actorIdentifier))
        {
            return BadRequest("Thiếu key cần tra cứu.");
        }

        if (string.IsNullOrWhiteSpace(filter.Action))
        {
            return BadRequest("Thiếu action cần tra cứu.");
        }

        var actorIdentifierType = ActorIdentityHelper.NormalizeType(filter.ActorIdentifierType, actorIdentifier);
        var localAlertDate = filter.AlertDate?.Date ?? VietnamTimeExtensions.TodayInVietnamDate();
        var fromUtc = VietnamTimeExtensions.VietnamDateToUtcStart(localAlertDate);
        var toUtc = VietnamTimeExtensions.VietnamDateToUtcStart(localAlertDate.AddDays(1)).AddTicks(-1);
        var normalizedPage = Math.Max(filter.Page, 1);
        const int normalizedPageSize = 10;
        var normalizedFilter = new AlertHistoryDetailsFilterViewModel
        {
            ActorIdentifier = actorIdentifier,
            ActorIdentifierType = actorIdentifierType,
            PartnerId = filter.PartnerId,
            Action = filter.Action.Trim(),
            AlertDate = localAlertDate,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        };

        var query = new LogQuery
        {
            PartnerId = normalizedFilter.PartnerId,
            Action = normalizedFilter.Action,
            ActorIdentifier = actorIdentifier,
            ActorIdentifierType = actorIdentifierType,
            FromUtc = fromUtc,
            ToUtc = toUtc,
            Page = normalizedPage,
            PageSize = normalizedPageSize
        };

        return PartialView("_AlertHistoryDetailsModalContent", new AlertHistoryDetailsViewModel
        {
            Filter = normalizedFilter,
            Logs = await _activityLogService.GetPagedAsync(query, cancellationToken),
            ActorIdentifier = actorIdentifier,
            ActorIdentifierType = actorIdentifierType,
            Action = normalizedFilter.Action,
            AlertDate = localAlertDate,
            ActorLabel = ActorIdentityHelper.BuildDisplayLabel(actorIdentifierType)
        });
    }
}
