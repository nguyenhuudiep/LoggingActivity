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

    public AlertHistoryController(AlertHistoryService alertHistoryService)
    {
        _alertHistoryService = alertHistoryService;
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
            FromUtc = filter.From?.Date,
            ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        return View(new AlertHistoryListViewModel
        {
            Filter = filter,
            Alerts = await _alertHistoryService.GetPagedAsync(query, cancellationToken)
        });
    }
}