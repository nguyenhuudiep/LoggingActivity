using System.Security.Claims;

namespace LoggingActivity.Web.Models;

public sealed class AdminFunctionPermissionDefinition
{
    public AdminFunctionPermissionDefinition(string code, string displayName, string shortDisplayName, string description, bool auditorEligible = false)
    {
        Code = code;
        DisplayName = displayName;
        ShortDisplayName = shortDisplayName;
        Description = description;
        AuditorEligible = auditorEligible;
    }

    public string Code { get; }

    public string DisplayName { get; }

    public string ShortDisplayName { get; }

    public string Description { get; }

    // Quyền chỉ-xem có thể cấp cho Auditor; quyền quản trị chỉ có hiệu lực với Admin.
    public bool AuditorEligible { get; }
}

public static class AdminFunctionPermissions
{
    public const string ClaimType = "function_permission";

    // Cookie phát hành trước khi đổi cơ chế phân quyền không có claim này và sẽ bị yêu cầu đăng nhập lại.
    public const string VersionClaimType = "function_permission_version";
    public const string CurrentVersion = "2";

    public const string UserManagement = "user_management";
    public const string PermissionGroupManagement = "permission_group_management";
    public const string LogDashboard = "log_dashboard";
    public const string SystemAccessHistory = "system_access_history";
    public const string IngestQueue = "ingest_queue";
    public const string AlertHistory = "alert_history";
    public const string CustomerBehaviorReport = "customer_behavior_report";
    public const string CitizenIdDetection = "citizen_id_detection";
    public const string VehicleRegistrationOcr = "vehicle_registration_ocr";
    public const string LogActionManagement = "log_action_management";
    public const string PartnerManagement = "partner_management";
    public const string PartnerActionLimit = "partner_action_limit";
    public const string AlertRuleManagement = "alert_rule_management";
    public const string IntegrationGuide = "integration_guide";

    // Mỗi quyền tương ứng đúng một menu để nhóm quyền tick gì thì hiển thị đúng menu đó.
    public static readonly IReadOnlyList<AdminFunctionPermissionDefinition> All = new[]
    {
        new AdminFunctionPermissionDefinition(UserManagement, "Quản lý tài khoản", "Tài khoản", "Xem danh sách, tạo mới và cập nhật tài khoản người dùng."),
        new AdminFunctionPermissionDefinition(LogDashboard, "Log và thống kê", "Log & Thống kê", "Truy cập màn hình log, bộ lọc, thống kê và cảnh báo active.", auditorEligible: true),
        new AdminFunctionPermissionDefinition(SystemAccessHistory, "Lịch sử truy cập hệ thống", "Lịch sử truy cập", "Theo dõi lịch sử truy cập và đăng nhập vào chính hệ thống Logging.", auditorEligible: true),
        new AdminFunctionPermissionDefinition(IngestQueue, "Hàng đợi ingest", "Hàng đợi ingest", "Theo dõi hàng đợi tiếp nhận log; Admin có thêm quyền retry và dọn dữ liệu demo.", auditorEligible: true),
        new AdminFunctionPermissionDefinition(AlertHistory, "Lịch sử cảnh báo", "Lịch sử cảnh báo", "Tra cứu toàn bộ lịch sử cảnh báo đã phát sinh theo thời gian.", auditorEligible: true),
        new AdminFunctionPermissionDefinition(CustomerBehaviorReport, "Báo cáo hành vi khách hàng", "Hành vi khách hàng", "Xem báo cáo hành vi khách hàng gọi API LOS: xu hướng, action, khung giờ và top khách hàng.", auditorEligible: true),
        new AdminFunctionPermissionDefinition(CitizenIdDetection, "Test nhận diện CCCD", "Test nhận diện CCCD", "Dùng màn hình thử nghiệm nhận diện mặt và OCR căn cước công dân.", auditorEligible: true),
        new AdminFunctionPermissionDefinition(VehicleRegistrationOcr, "OCR đăng ký xe", "OCR đăng ký xe", "Dùng màn hình thử nghiệm OCR giấy đăng ký xe.", auditorEligible: true),
        new AdminFunctionPermissionDefinition(IntegrationGuide, "Hướng dẫn tích hợp API", "Hướng dẫn API", "Xem tài liệu chi tiết, curl mẫu và payload example cho API tích hợp.", auditorEligible: true),
        new AdminFunctionPermissionDefinition(LogActionManagement, "Action log", "Action log", "Quản lý danh mục action log và trạng thái từng action."),
        new AdminFunctionPermissionDefinition(PermissionGroupManagement, "Nhóm quyền", "Nhóm quyền", "Tạo và cập nhật nhóm quyền, quyết định menu mà từng nhóm được thấy."),
        new AdminFunctionPermissionDefinition(PartnerManagement, "Partner", "Partner", "Quản lý danh sách partner, trạng thái và API key tích hợp."),
        new AdminFunctionPermissionDefinition(PartnerActionLimit, "Hạn mức user-action", "Hạn mức user-action", "Cấu hình hạn mức số lần gọi action theo partner và user."),
        new AdminFunctionPermissionDefinition(AlertRuleManagement, "Cảnh báo log", "Cảnh báo log", "Thiết lập ngưỡng cảnh báo theo action và quản lý rule cảnh báo.")
    };

    public static IReadOnlyList<string> AllCodes { get; } = All.Select(permission => permission.Code).ToList();

    // Lọc danh sách quyền hợp lệ theo role: Admin nhận mọi quyền, Auditor chỉ nhận quyền chỉ-xem.
    public static List<string> FilterForRole(string? role, IEnumerable<string>? permissions)
    {
        var requested = permissions?
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var isAdmin = string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase);
        var isAuditor = string.Equals(role, SystemRoles.Auditor, StringComparison.OrdinalIgnoreCase);
        if (!isAdmin && !isAuditor)
        {
            return new List<string>();
        }

        return All
            .Where(permission => requested.Contains(permission.Code) && (isAdmin || permission.AuditorEligible))
            .Select(permission => permission.Code)
            .ToList();
    }
}

public static class UserPermissionExtensions
{
    private static bool HasPermissionClaim(ClaimsPrincipal user, string permission)
    {
        return user.FindAll(AdminFunctionPermissions.ClaimType)
            .Any(claim => string.Equals(claim.Value, permission, StringComparison.OrdinalIgnoreCase));
    }

    // Quyền quản trị: chỉ Admin có đúng quyền được tick.
    public static bool HasAdminFunctionPermission(this ClaimsPrincipal user, string permission)
    {
        return user.Identity?.IsAuthenticated == true
            && user.IsInRole(SystemRoles.Admin)
            && HasPermissionClaim(user, permission);
    }

    // Trang chỉ-xem: Admin có quyền, hoặc Auditor có quyền khi trang cho phép Auditor.
    public static bool HasFeatureAccess(this ClaimsPrincipal user, string permission, bool allowAuditor = false)
    {
        if (user.HasAdminFunctionPermission(permission))
        {
            return true;
        }

        return allowAuditor
            && user.Identity?.IsAuthenticated == true
            && user.IsInRole(SystemRoles.Auditor)
            && HasPermissionClaim(user, permission);
    }
}
