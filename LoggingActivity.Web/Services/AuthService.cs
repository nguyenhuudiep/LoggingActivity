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
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id ?? string.Empty),
            new(ClaimTypes.Name, user.UserName),
            new(ClaimTypes.GivenName, user.DisplayName),
            new(ClaimTypes.Role, user.Role)
        };

        if (string.Equals(user.Role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var groupPermissions = await _permissionGroupService.ResolveActiveFunctionPermissionsAsync(user.PermissionGroupIds);
            var customPermissions = user.CustomFunctionPermissions.Count > 0
                ? user.CustomFunctionPermissions
                : user.FunctionPermissions;
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