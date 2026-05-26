using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;

namespace LoggingActivity.Web.Controllers;

[Authorize]
public class HomeController : AppController
{
    private readonly ILogger<HomeController> _logger;
    private readonly AlertRuleService _alertRuleService;
    private readonly ActivityLogService _activityLogService;

    public HomeController(ILogger<HomeController> logger, AlertRuleService alertRuleService, ActivityLogService activityLogService)
    {
        _logger = logger;
        _alertRuleService = alertRuleService;
        _activityLogService = activityLogService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        ViewData["DisplayName"] = User.FindFirst(System.Security.Claims.ClaimTypes.GivenName)?.Value ?? User.Identity?.Name;
        return View(new HomeDashboardViewModel
        {
            OverviewStatistics = await _activityLogService.GetStatisticsAsync(new LogQuery
            {
                FromUtc = today.AddDays(-6),
                ToUtc = today.AddDays(1).AddTicks(-1),
                Page = 1,
                PageSize = 10
            }, cancellationToken),
            ActiveWarnings = await _alertRuleService.GetActiveWarningsAsync(cancellationToken),
            UnconfiguredActionWarnings = await _alertRuleService.GetUnconfiguredActionWarningsAsync(cancellationToken)
        });
    }

    [Authorize(Roles = SystemRoles.Admin + "," + SystemRoles.Auditor)]
    public IActionResult IntegrationGuide()
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.IntegrationGuide, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        return View();
    }

    [AllowAnonymous]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
