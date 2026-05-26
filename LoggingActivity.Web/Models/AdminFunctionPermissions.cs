using System.Security.Claims;

namespace LoggingActivity.Web.Models;

public sealed class AdminFunctionPermissionDefinition
{
    public AdminFunctionPermissionDefinition(string code, string displayName, string shortDisplayName, string description)
    {
        Code = code;
        DisplayName = displayName;
        ShortDisplayName = shortDisplayName;
        Description = description;
    }

    public string Code { get; }

    public string DisplayName { get; }

    public string ShortDisplayName { get; }

    public string Description { get; }
}

public static class AdminFunctionPermissions
{
    public const string ClaimType = "function_permission";

    public const string UserManagement = "user_management";
    public const string LogDashboard = "log_dashboard";
    public const string AlertHistory = "alert_history";
    public const string LogActionManagement = "log_action_management";
    public const string PartnerManagement = "partner_management";
    public const string AlertRuleManagement = "alert_rule_management";
    public const string IntegrationGuide = "integration_guide";

    public static readonly IReadOnlyList<AdminFunctionPermissionDefinition> All = new[]
    {
        new AdminFunctionPermissionDefinition(UserManagement, "Quản lý tài khoản", "Tài khoản", "Xem danh sách, tạo mới và cập nhật tài khoản người dùng."),
        new AdminFunctionPermissionDefinition(LogDashboard, "Log và thống kê", "Dashboard log", "Truy cập màn hình log, bộ lọc, thống kê và cảnh báo active."),
        new AdminFunctionPermissionDefinition(AlertHistory, "Lịch sử cảnh báo", "Lịch sử cảnh báo", "Tra cứu toàn bộ lịch sử cảnh báo đã phát sinh theo thời gian."),
        new AdminFunctionPermissionDefinition(LogActionManagement, "Action log", "Danh mục action", "Quản lý danh mục action log và trạng thái từng action."),
        new AdminFunctionPermissionDefinition(PartnerManagement, "Partner", "Partner", "Quản lý danh sách partner, trạng thái và API key tích hợp."),
        new AdminFunctionPermissionDefinition(AlertRuleManagement, "Cảnh báo log", "Rule cảnh báo", "Thiết lập ngưỡng cảnh báo theo action và quản lý rule cảnh báo."),
        new AdminFunctionPermissionDefinition(IntegrationGuide, "Hướng dẫn tích hợp API", "Hướng dẫn API", "Xem tài liệu chi tiết, curl mẫu và payload example cho API tích hợp.")
    };
}

public static class UserPermissionExtensions
{
    public static bool HasAdminFunctionPermission(this ClaimsPrincipal user, string permission)
    {
        if (user.Identity?.IsAuthenticated != true || !user.IsInRole(SystemRoles.Admin))
        {
            return false;
        }

        var grantedPermissions = user.FindAll(AdminFunctionPermissions.ClaimType)
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return grantedPermissions.Count == 0 || grantedPermissions.Contains(permission);
    }

    public static bool HasFeatureAccess(this ClaimsPrincipal user, string permission, bool allowAuditor = false)
    {
        if (user.HasAdminFunctionPermission(permission))
        {
            return true;
        }

        return allowAuditor && user.IsInRole(SystemRoles.Auditor);
    }
}