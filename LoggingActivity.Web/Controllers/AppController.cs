using LoggingActivity.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

public abstract class AppController : Controller
{
    protected IActionResult? ForbidIfMissingPermission(string permission, bool allowAuditor = false)
    {
        return User.HasFeatureAccess(permission, allowAuditor) ? null : Forbid();
    }
}