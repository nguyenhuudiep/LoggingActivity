using Microsoft.AspNetCore.Mvc.Controllers;
using LoggingActivity.Web.Services;

namespace LoggingActivity.Web.Middleware;

public sealed class SystemAccessAuditMiddleware
{
    private readonly RequestDelegate _next;

    public SystemAccessAuditMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, SystemAccessAuditService systemAccessAuditService)
    {
        await _next(context);

        if (context.User.Identity?.IsAuthenticated != true)
        {
            return;
        }

        if (ShouldSkip(context))
        {
            return;
        }

        await systemAccessAuditService.RecordAccessAsync(context, context.RequestAborted);
    }

    private static bool ShouldSkip(HttpContext context)
    {
        if (HttpMethods.IsPost(context.Request.Method)
            || HttpMethods.IsPut(context.Request.Method)
            || HttpMethods.IsDelete(context.Request.Method)
            || HttpMethods.IsPatch(context.Request.Method))
        {
            return true;
        }

        var endpoint = context.GetEndpoint();
        if (endpoint?.Metadata.GetMetadata<ControllerActionDescriptor>() is null)
        {
            return true;
        }

        var path = context.Request.Path;
        if (path.StartsWithSegments("/Auth/Logout", StringComparison.OrdinalIgnoreCase)
            || path.StartsWithSegments("/Auth/Login", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}
