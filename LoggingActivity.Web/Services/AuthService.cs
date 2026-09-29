using System.Security.Claims;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using MongoDB.Driver.Core;

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

    // Quyền hiệu lực = quyền riêng của tài khoản + quyền của các nhóm đang active, tính lại mỗi lần đăng nhập.
    // Riêng tài khoản seed admin luôn có toàn quyền để không bao giờ bị khóa khỏi hệ thống.
    private async Task<IReadOnlyList<string>> ResolveEffectivePermissionsAsync(AppUser user, string userName, string role, CancellationToken cancellationToken)
    {
        if (IsSeedAdmin(userName) && string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return AdminFunctionPermissions.AllCodes;
        }

        var permissionGroupIds = user.PermissionGroupIds ?? new List<string>();
        var customPermissions = user.CustomFunctionPermissions ?? new List<string>();

        // Tài khoản cũ (trước khi có nhóm quyền) chỉ lưu FunctionPermissions; khi đã có nhóm thì không dùng bản chụp này
        // vì nó có thể chứa quyền của nhóm từ trước khi nhóm bị bỏ tick.
        if (customPermissions.Count == 0 && permissionGroupIds.Count == 0)
        {
            customPermissions = user.FunctionPermissions ?? new List<string>();
        }

        var groupPermissions = permissionGroupIds.Count == 0
            ? new List<string>()
            : await _permissionGroupService.ResolveActiveFunctionPermissionsAsync(permissionGroupIds, cancellationToken);

        return AdminFunctionPermissions.FilterForRole(role, customPermissions.Concat(groupPermissions));
    }

    private bool IsSeedAdmin(string userName)
    {
        var seedUserName = _seedAdminOptions.Value.UserName;
        return !string.IsNullOrWhiteSpace(seedUserName)
            && string.Equals(seedUserName.Trim(), userName, StringComparison.OrdinalIgnoreCase);
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
        var effectivePermissions = await ResolveEffectivePermissionsAsync(user, safeUserName, safeRole, httpContext.RequestAborted);

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, string.IsNullOrWhiteSpace(user.Id) ? safeUserName : user.Id),
            new(ClaimTypes.Name, safeUserName),
            new(ClaimTypes.GivenName, safeDisplayName),
            new(ClaimTypes.Role, safeRole),
            new(SystemAccessAuditService.SessionClaimType, sessionId),
            new(AdminFunctionPermissions.VersionClaimType, AdminFunctionPermissions.CurrentVersion)
        };

        claims.AddRange(effectivePermissions.Select(permission => new Claim(AdminFunctionPermissions.ClaimType, permission)));

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
                        new Claim(SystemAccessAuditService.SessionClaimType, sessionId),
                        new Claim(AdminFunctionPermissions.VersionClaimType, AdminFunctionPermissions.CurrentVersion)
                    },
                    CookieAuthenticationDefaults.AuthenticationScheme));

            await httpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                minimalPrincipal,
                new AuthenticationProperties { IsPersistent = isPersistent });
        }

        try
        {
            var replacedExistingSession = await _systemAccessAuditService.ActivateSessionAsync(httpContext, user, sessionId, httpContext.RequestAborted);
            await _systemAccessAuditService.RecordLoginAsync(httpContext, user, sessionId, replacedExistingSession, httpContext.RequestAborted);
        }
        catch (Exception ex) when (IsMongoUnavailable(ex))
        {
            _logger.LogWarning(ex, "MongoDB unavailable while writing login audit for user {UserName}. Continue sign-in without audit persistence.", safeUserName);
        }
    }

    public async Task SignOutAsync(HttpContext httpContext)
    {
        try
        {
            await _systemAccessAuditService.RecordLogoutAsync(httpContext, httpContext.RequestAborted);
        }
        catch (Exception ex) when (IsMongoUnavailable(ex))
        {
            _logger.LogWarning(ex, "MongoDB unavailable while writing logout audit.");
        }

        var userName = httpContext.User.Identity?.Name;
        var sessionId = httpContext.User.FindFirst(SystemAccessAuditService.SessionClaimType)?.Value;
        try
        {
            await _systemAccessAuditService.DeactivateSessionAsync(userName, sessionId, httpContext.RequestAborted);
        }
        catch (Exception ex) when (IsMongoUnavailable(ex))
        {
            _logger.LogWarning(ex, "MongoDB unavailable while deactivating session for user {UserName}.", userName);
        }

        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    private static bool IsMongoUnavailable(Exception exception)
    {
        if (exception is TimeoutException or MongoConnectionException or MongoException)
        {
            return true;
        }

        return exception.InnerException is not null && IsMongoUnavailable(exception.InnerException);
    }
}