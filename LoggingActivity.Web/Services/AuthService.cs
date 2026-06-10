using System.Security.Claims;
using LoggingActivity.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace LoggingActivity.Web.Services;

public sealed class AuthService
{
    private readonly UserService _userService;
    private readonly PermissionGroupService _permissionGroupService;

    public AuthService(UserService userService, PermissionGroupService permissionGroupService)
    {
        _userService = userService;
        _permissionGroupService = permissionGroupService;
    }

    public async Task<AppUser?> ValidateCredentialsAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var user = await _userService.GetByUserNameAsync(userName, cancellationToken);
        if (user is null || !user.IsActive)
        {
            return null;
        }

        return await _userService.VerifyPasswordAsync(user, password) ? user : null;
    }

    public async Task SignInAsync(HttpContext httpContext, AppUser user, bool isPersistent)
    {
        var safeUserName = string.IsNullOrWhiteSpace(user.UserName) ? "unknown-user" : user.UserName.Trim();
        var safeDisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? safeUserName : user.DisplayName.Trim();
        var safeRole = string.IsNullOrWhiteSpace(user.Role) ? SystemRoles.Auditor : user.Role.Trim();
        var safePermissionGroupIds = user.PermissionGroupIds ?? new List<string>();
        var safeCustomPermissions = user.CustomFunctionPermissions ?? new List<string>();
        var safeFunctionPermissions = user.FunctionPermissions ?? new List<string>();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, string.IsNullOrWhiteSpace(user.Id) ? safeUserName : user.Id),
            new(ClaimTypes.Name, safeUserName),
            new(ClaimTypes.GivenName, safeDisplayName),
            new(ClaimTypes.Role, safeRole)
        };

        if (string.Equals(safeRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var groupPermissions = await _permissionGroupService.ResolveActiveFunctionPermissionsAsync(safePermissionGroupIds);
            var customPermissions = safeCustomPermissions.Count > 0
                ? safeCustomPermissions
                : safeFunctionPermissions;
            var effectivePermissions = customPermissions.Concat(groupPermissions);

            claims.AddRange(effectivePermissions
                .Where(permission => !string.IsNullOrWhiteSpace(permission))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(permission => new Claim(AdminFunctionPermissions.ClaimType, permission)));
        }

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        await httpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties { IsPersistent = isPersistent });
    }

    public Task SignOutAsync(HttpContext httpContext)
    {
        return httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}