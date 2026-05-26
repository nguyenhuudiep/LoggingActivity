using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;
using Microsoft.AspNetCore.Identity;

namespace LoggingActivity.Web.Services;

public sealed class UserService
{
    private readonly IUserRepository _userRepository;
    private readonly PasswordHasher<AppUser> _passwordHasher = new();

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
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
        existingUser.IsActive = user.IsActive;

        if (!string.IsNullOrWhiteSpace(newPassword))
        {
            existingUser.PasswordHash = _passwordHasher.HashPassword(existingUser, newPassword);
        }

        await _userRepository.UpdateAsync(existingUser, cancellationToken);
        return (true, null);
    }

    public Task<bool> VerifyPasswordAsync(AppUser user, string password)
    {
        var result = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        return Task.FromResult(result != PasswordVerificationResult.Failed);
    }
}