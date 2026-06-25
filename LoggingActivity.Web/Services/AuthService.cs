using System.Security.Claims;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;

namespace LoggingActivity.Web.Services;

public sealed class AuthService
{
    private readonly UserService _userService;
    private readonly PermissionGroupService _permissionGroupService;
    private readonly SystemAccessAuditService _systemAccessAuditService;
    private readonly IOptions<SeedAdminOptions> _seedAdminOptions;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        UserService userService,
        PermissionGroupService permissionGroupService,
        SystemAccessAuditService systemAccessAuditService,
        IOptions<SeedAdminOptions> seedAdminOptions,
        ILogger<AuthService> logger)
    {
        _userService = userService;
        _permissionGroupService = permissionGroupService;
        _systemAccessAuditService = systemAccessAuditService;
        _seedAdminOptions = seedAdminOptions;
        _logger = logger;
    }

    public async Task<AppUser?> ValidateCredentialsAsync(string userName, string password, CancellationToken cancellationToken = default)
    {
        var normalizedUserName = string.IsNullOrWhiteSpace(userName) ? string.Empty : userName.Trim();
        var seedAdminUser = TryValidateSeedAdmin(normalizedUserName, password);
        if (seedAdminUser is not null)
        {
            return seedAdminUser;
        }

        try
        {
            var user = await _userService.GetByUserNameAsync(normalizedUserName, cancellationToken);
            if (user is null || !user.IsActive)
            {
                return null;
            }

            return await _userService.VerifyPasswordAsync(user, password) ? user : null;
        }
        catch (Exception ex)
        {
            var fallbackSeedAdminUser = TryValidateSeedAdmin(normalizedUserName, password);
            if (fallbackSeedAdminUser is not null)
            {
                _logger.LogWarning(ex, "Primary credential validation failed for user {UserName}. SeedAdmin fallback was used.", normalizedUserName);
                return fallbackSeedAdminUser;
            }

            _logger.LogError(ex, "Credential validation failed for user {UserName}.", normalizedUserName);
            throw;
        }
    }

    private AppUser? TryValidateSeedAdmin(string userName, string password)
    {
        var options = _seedAdminOptions.Value;
        if (string.IsNullOrWhiteSpace(options.UserName)
            || string.IsNullOrWhiteSpace(options.Password))
        {
            return null;
        }

        if (!string.Equals(options.UserName.Trim(), userName, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(options.Password, password, StringComparison.Ordinal))
        {
            return null;
        }

        return new AppUser
        {
            UserName = options.UserName.Trim(),
            DisplayName = "Seed Admin",
            Email = string.IsNullOrWhiteSpace(options.Email) ? string.Empty : options.Email.Trim(),
            Role = SystemRoles.Admin,
            FunctionPermissions = AdminFunctionPermissions.All.Select(permission => permission.Code).ToList(),
            CustomFunctionPermissions = new List<string>(),
            PermissionGroupIds = new List<string>(),
            IsActive = true
        };
    }

    public async Task SignInAsync(HttpContext httpContext, AppUser user, bool isPersistent)
    {
        var safeUserName = string.IsNullOrWhiteSpace(user.UserName) ? "unknown-user" : user.UserName.Trim();
        var safeDisplayName = string.IsNullOrWhiteSpace(user.DisplayName) ? safeUserName : user.DisplayName.Trim();
        var safeRole = string.IsNullOrWhiteSpace(user.Role) ? SystemRoles.Auditor : user.Role.Trim();
        var sessionId = Guid.NewGuid().ToString("N");
        var safePermissionGroupIds = user.PermissionGroupIds ?? new List<string>();
        var safeCustomPermissions = user.CustomFunctionPermissions ?? new List<string>();
        var safeFunctionPermissions = user.FunctionPermissions ?? new List<string>();

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, string.IsNullOrWhiteSpace(user.Id) ? safeUserName : user.Id),
            new(ClaimTypes.Name, safeUserName),
            new(ClaimTypes.GivenName, safeDisplayName),
            new(ClaimTypes.Role, safeRole),
            new(SystemAccessAuditService.SessionClaimType, sessionId)
        };

        if (string.Equals(safeRole, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var allowedPermissions = AdminFunctionPermissions.All
                    .Select(permission => permission.Code)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                var groupPermissions = await _permissionGroupService.ResolveActiveFunctionPermissionsAsync(safePermissionGroupIds);
                var customPermissions = safeCustomPermissions.Count > 0
                    ? safeCustomPermissions
                    : safeFunctionPermissions;
                var effectivePermissions = customPermissions.Concat(groupPermissions);

                claims.AddRange(effectivePermissions
                    .Where(permission => !string.IsNullOrWhiteSpace(permission))
                    .Select(permission => permission.Trim())
                    .Where(permission => allowedPermissions.Contains(permission))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Select(permission => new Claim(AdminFunctionPermissions.ClaimType, permission)));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to resolve function permissions for user {UserName}", safeUserName);
            }
        }

        var principal = new ClaimsPrincipal(
            new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));

        try
        {
            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                new AuthenticationProperties { IsPersistent = isPersistent });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Primary sign-in failed for user {UserName}. Retrying with minimal claims.", safeUserName);

            var minimalPrincipal = new ClaimsPrincipal(
                new ClaimsIdentity(
                    new[]
                    {
                        new Claim(ClaimTypes.NameIdentifier, string.IsNullOrWhiteSpace(user.Id) ? safeUserName : user.Id),
                        new Claim(ClaimTypes.Name, safeUserName),
                        new Claim(ClaimTypes.GivenName, safeDisplayName),
                        new Claim(ClaimTypes.Role, safeRole),
                        new Claim(SystemAccessAuditService.SessionClaimType, sessionId)
                    },
                    CookieAuthenticationDefaults.AuthenticationScheme));

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                minimalPrincipal,
                new AuthenticationProperties { IsPersistent = isPersistent });
        }

        var replacedExistingSession = await _systemAccessAuditService.ActivateSessionAsync(httpContext, user, sessionId, httpContext.RequestAborted);
        await _systemAccessAuditService.RecordLoginAsync(httpContext, user, sessionId, replacedExistingSession, httpContext.RequestAborted);
    }

    public async Task SignOutAsync(HttpContext httpContext)
    {
        await _systemAccessAuditService.RecordLogoutAsync(httpContext, httpContext.RequestAborted);

        var userName = httpContext.User.Identity?.Name;
        var sessionId = httpContext.User.FindFirst(SystemAccessAuditService.SessionClaimType)?.Value;
        await _systemAccessAuditService.DeactivateSessionAsync(userName, sessionId, httpContext.RequestAborted);

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}