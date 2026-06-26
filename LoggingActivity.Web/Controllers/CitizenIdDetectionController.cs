using LoggingActivity.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin + "," + SystemRoles.Auditor)]
public sealed class CitizenIdDetectionController : AppController
{
    [HttpGet]
    public IActionResult Index()
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.IntegrationGuide, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        return View();
    }
}
