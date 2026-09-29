using LoggingActivity.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace LoggingActivity.Web.Infrastructure;

// Kiểm tra quyền chức năng cho các API controller (không kế thừa AppController); chạy trước model binding.
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public sealed class RequireFunctionPermissionAttribute : Attribute, IAuthorizationFilter
{
    public RequireFunctionPermissionAttribute(string permission)
    {
        Permission = permission;
    }

    public string Permission { get; }

    public bool AllowAuditor { get; init; }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var user = context.HttpContext.User;
        if (user.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (!user.HasFeatureAccess(Permission, AllowAuditor))
        {
            context.Result = new ForbidResult();
        }
    }
}
