using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;
using Microsoft.AspNetCore.Identity;

namespace LoggingActivity.Web.Services;

public sealed class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly PermissionGroupService _permissionGroupService;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public UserService(IUserRepository userRepository, PermissionGroupService permissionGroupService)
    {
        _userRepository = userRepository;
        _permissionGroupService = permissionGroupService;
    }

    public Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _userRepository.GetAllAsync(cancellationToken);
    }

    public Task<PagedResult<AppUser>> GetPagedAsync(UserQuery query, CancellationToken cancellationToken = default)
    {
        return _userRepository.GetPagedAsync(query, cancellationToken);
    }

    public Task<UserStatistics> GetStatisticsAsync(UserQuery query, CancellationToken cancellationToken = default)
    {
        return _userRepository.GetStatisticsAsync(query, cancellationToken);
    }

    public Task<AppUser?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return _userRepository.GetByIdAsync(id, cancellationToken);
    }

    public Task<AppUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        return _userRepository.GetByUserNameAsync(userName, cancellationToken);
    }

    public async Task<(bool Success, string? Error)> CreateAsync(AppUser user, string password, CancellationToken cancellationToken = default)
    {
        var existingUser = await _userRepository.GetByUserNameAsync(user.UserName, cancellationToken);
        if (existingUser is not null)
        {
            return (false, "Tên đăng nhập đã tồn tại.");
        }

        user.PermissionGroupIds = await NormalizePermissionGroupIdsAsync(user.Role, user.PermissionGroupIds, cancellationToken);
        var requestedPermissions = NormalizeFunctionPermissions(user.Role, user.FunctionPermissions);
        var groupPermissions = await _permissionGroupService.ResolveActiveFunctionPermissionsAsync(user.PermissionGroupIds, cancellationToken);
        user.CustomFunctionPermissions = requestedPermissions
            .Except(groupPermissions, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        user.FunctionPermissions = requestedPermissions;
        user.PasswordHash = _passwordHasher.HashPassword(user, password);
        user.CreatedAtUtc = DateTime.UtcNow;
        user.UpdatedAtUtc = DateTime.UtcNow;

        await _userRepository.CreateAsync(user, cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(AppUser user, string? newPassword, CancellationToken cancellationToken = default)
    {
        var existingUser = await _userRepository.GetByIdAsync(user.Id!, cancellationToken);
        if (existingUser is null)
        {
            return (false, "Không tìm thấy tài khoản.");
        }

        existingUser.DisplayName = user.DisplayName;
        existingUser.Email = user.Email;
        existingUser.Role = user.Role;
        existingUser.PermissionGroupIds = await NormalizePermissionGroupIdsAsync(user.Role, user.PermissionGroupIds, cancellationToken);
        var requestedPermissions = NormalizeFunctionPermissions(user.Role, user.FunctionPermissions);
        var groupPermissions = await _permissionGroupService.ResolveActiveFunctionPermissionsAsync(existingUser.PermissionGroupIds, cancellationToken);
        existingUser.CustomFunctionPermissions = requestedPermissions
            .Except(groupPermissions, StringComparer.OrdinalIgnoreCase)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        existingUser.FunctionPermissions = requestedPermissions;
        existingUser.IsActive = user.IsActive;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            existingUser.PasswordHash = _passwordHasher.HashPassword(existingUser, newPassword);
        }

        await _userRepository.UpdateAsync(existingUser, cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> DeleteAsync(string id, string? currentUserName, CancellationToken cancellationToken = default)
    {
        var existingUser = await _userRepository.GetByIdAsync(id, cancellationToken);
        if (existingUser is null)
        {
            return (false, "Không tìm thấy tài khoản.");
        }

        if (!string.IsNullOrWhiteSpace(currentUserName)
            && string.Equals(existingUser.UserName, currentUserName.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            return (false, "Không thể xóa tài khoản đang đăng nhập.");
        }

        if (string.Equals(existingUser.Role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            var adminCount = await _userRepository.CountByRoleAsync(SystemRoles.Admin, cancellationToken);
            if (adminCount <= 1)
            {
                return (false, "Không thể xóa tài khoản admin cuối cùng của hệ thống.");
            }
        }

        await _userRepository.DeleteAsync(id, cancellationToken);
        return (true, null);
    }

    public Task<bool> VerifyPasswordAsync(AppUser user, string password)
    {
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return Task.FromResult(result != PasswordVerificationResult.Failed);
    }

    private static List<string> NormalizeFunctionPermissions(string role, IEnumerable<string>? permissions)
    {
        if (!string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return new List<string>();
        }

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

    private async Task<List<string>> NormalizePermissionGroupIdsAsync(string role, IEnumerable<string>? permissionGroupIds, CancellationToken cancellationToken)
    {
        if (!string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase))
        {
            return new List<string>();
        }

        return await _permissionGroupService.NormalizePermissionGroupIdsAsync(permissionGroupIds, cancellationToken);
    }
}