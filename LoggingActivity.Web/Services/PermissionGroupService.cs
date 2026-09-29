using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;

namespace LoggingActivity.Web.Services;

public sealed class PermissionGroupService
{
    private readonly IPermissionGroupRepository _permissionGroupRepository;
    private readonly IUserRepository _userRepository;
    private readonly SystemAccessAuditService _systemAccessAuditService;

    public PermissionGroupService(
        IPermissionGroupRepository permissionGroupRepository,
        IUserRepository userRepository,
        SystemAccessAuditService systemAccessAuditService)
    {
        _permissionGroupRepository = permissionGroupRepository;
        _userRepository = userRepository;
        _systemAccessAuditService = systemAccessAuditService;
    }

    public Task<PagedResult<PermissionGroup>> GetPagedAsync(PermissionGroupQuery query, CancellationToken cancellationToken = default)
    {
        return _permissionGroupRepository.GetPagedAsync(query, cancellationToken);
    }

    public Task<PermissionGroupStatistics> GetStatisticsAsync(PermissionGroupQuery query, CancellationToken cancellationToken = default)
    {
        return _permissionGroupRepository.GetStatisticsAsync(query, cancellationToken);
    }

    public Task<PermissionGroup?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return _permissionGroupRepository.GetByIdAsync(id, cancellationToken);
    }

    public Task<IReadOnlyList<PermissionGroup>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return _permissionGroupRepository.GetActiveAsync(cancellationToken);
    }

    public Task<IReadOnlyList<PermissionGroup>> GetByIdsAsync(IEnumerable<string> ids, CancellationToken cancellationToken = default)
    {
        return _permissionGroupRepository.GetByIdsAsync(ids, cancellationToken);
    }

    public async Task<List<string>> NormalizePermissionGroupIdsAsync(IEnumerable<string>? ids, CancellationToken cancellationToken = default)
    {
        var requestedIds = ids?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToList()
            ?? new List<string>();

        if (requestedIds.Count == 0)
        {
            return new List<string>();
        }

        var groups = await _permissionGroupRepository.GetByIdsAsync(requestedIds, cancellationToken);
        return requestedIds
            .Where(id => groups.Any(group => string.Equals(group.Id, id, StringComparison.Ordinal)))
            .ToList();
    }

    public async Task<List<string>> ResolveActiveFunctionPermissionsAsync(IEnumerable<string>? ids, CancellationToken cancellationToken = default)
    {
        var groups = await _permissionGroupRepository.GetByIdsAsync(ids ?? Array.Empty<string>(), cancellationToken);
        var requestedPermissions = groups
            .Where(group => group.IsActive)
            .SelectMany(group => group.FunctionPermissions)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return AdminFunctionPermissions.All
            .Where(permission => requestedPermissions.Contains(permission.Code))
            .Select(permission => permission.Code)
            .ToList();
    }

    public async Task<(bool Success, string? Error)> CreateAsync(PermissionGroup group, CancellationToken cancellationToken = default)
    {
        var existingGroup = await _permissionGroupRepository.GetByNameAsync(group.Name, cancellationToken);
        if (existingGroup is not null)
        {
            return (false, "Tên nhóm quyền đã tồn tại.");
        }

        group.Name = group.Name.Trim();
        group.Description = group.Description.Trim();
        group.FunctionPermissions = NormalizeFunctionPermissions(group.FunctionPermissions);
        group.CreatedAtUtc = DateTime.UtcNow;
        group.UpdatedAtUtc = DateTime.UtcNow;

        await _permissionGroupRepository.CreateAsync(group, cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(PermissionGroup group, CancellationToken cancellationToken = default)
    {
        var existingGroup = await _permissionGroupRepository.GetByIdAsync(group.Id!, cancellationToken);
        if (existingGroup is null)
        {
            return (false, "Không tìm thấy nhóm quyền.");
        }

        var duplicateGroup = await _permissionGroupRepository.GetByNameAsync(group.Name, cancellationToken);
        if (duplicateGroup is not null && !string.Equals(duplicateGroup.Id, group.Id, StringComparison.Ordinal))
        {
            return (false, "Tên nhóm quyền đã tồn tại.");
        }

        existingGroup.Name = group.Name.Trim();
        existingGroup.Description = group.Description.Trim();
        existingGroup.FunctionPermissions = NormalizeFunctionPermissions(group.FunctionPermissions);
        existingGroup.IsActive = group.IsActive;

        await _permissionGroupRepository.UpdateAsync(existingGroup, cancellationToken);
        await SyncAssignedUsersAsync(existingGroup.Id!, cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        var existingGroup = await _permissionGroupRepository.GetByIdAsync(id, cancellationToken);
        if (existingGroup is null)
        {
            return (false, "Không tìm thấy nhóm quyền.");
        }

        var assignedUserCount = await _userRepository.CountByPermissionGroupIdAsync(id, cancellationToken);
        if (assignedUserCount > 0)
        {
            return (false, $"Không thể xóa nhóm quyền đang được gán cho {assignedUserCount} tài khoản.");
        }

        await _permissionGroupRepository.DeleteAsync(id, cancellationToken);
        return (true, null);
    }

    private static List<string> NormalizeFunctionPermissions(IEnumerable<string>? permissions)
    {
        var requestedPermissions = permissions?
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Select(code => code.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase)
            ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        return AdminFunctionPermissions.All
            .Where(permission => requestedPermissions.Contains(permission.Code))
            .Select(permission => permission.Code)
            .ToList();
    }

    // Cập nhật bản chụp FunctionPermissions (quyền riêng + quyền nhóm hiện tại) cho thành viên của nhóm,
    // không đụng tới quyền riêng, rồi buộc họ đăng nhập lại để menu phản ánh ngay thay đổi của nhóm.
    private async Task SyncAssignedUsersAsync(string permissionGroupId, CancellationToken cancellationToken)
    {
        var assignedUsers = await _userRepository.GetByPermissionGroupIdAsync(permissionGroupId, cancellationToken);
        if (assignedUsers.Count == 0)
        {
            return;
        }

        foreach (var user in assignedUsers)
        {
            var currentGroupPermissions = await ResolveActiveFunctionPermissionsAsync(user.PermissionGroupIds, cancellationToken);
            user.FunctionPermissions = AdminFunctionPermissions.FilterForRole(
                user.Role,
                user.CustomFunctionPermissions.Concat(currentGroupPermissions));

            await _userRepository.UpdateAsync(user, cancellationToken);
        }

        await _systemAccessAuditService.RevokeSessionsAsync(assignedUsers.Select(user => user.UserName), cancellationToken);
    }
}