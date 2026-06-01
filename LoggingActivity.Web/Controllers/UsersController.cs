using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin)]
public sealed class UsersController : AppController
{
    private readonly UserService _userService;
    private readonly PermissionGroupService _permissionGroupService;

    public UsersController(UserService userService, PermissionGroupService permissionGroupService)
    {
        _userService = userService;
        _permissionGroupService = permissionGroupService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] UserFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.UserManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var query = new UserQuery
        {
            SearchTerm = filter.SearchTerm,
            Role = filter.Role,
            IsActive = filter.IsActive,
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        var users = await _userService.GetPagedAsync(query, cancellationToken);
        var statistics = await _userService.GetStatisticsAsync(query, cancellationToken);

        return View(new UserManagementViewModel
        {
            Filter = filter,
            Users = users,
            Statistics = statistics
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create(CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.UserManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        return View(new UserEditViewModel
        {
            IsCreateMode = true,
            IsActive = true,
            AvailablePermissionGroups = await _permissionGroupService.GetActiveAsync(cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserEditViewModel model, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.UserManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        model.IsCreateMode = true;
        await PopulatePermissionGroupsAsync(model, cancellationToken);
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _userService.CreateAsync(new AppUser
        {
            UserName = model.UserName.Trim(),
            DisplayName = model.DisplayName.Trim(),
            Email = model.Email.Trim(),
            Role = model.Role,
            PermissionGroupIds = model.SelectedPermissionGroupIds,
            FunctionPermissions = model.SelectedPermissions,
            IsActive = model.IsActive
        }, model.Password!, cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["StatusMessage"] = "Tạo tài khoản thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.UserManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var user = await _userService.GetByIdAsync(id, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }

        return View(new UserEditViewModel
        {
            Id = user.Id,
            UserName = user.UserName,
            DisplayName = user.DisplayName,
            Email = user.Email,
            Role = user.Role,
            SelectedPermissionGroupIds = user.PermissionGroupIds.ToList(),
            AvailablePermissionGroups = await _permissionGroupService.GetActiveAsync(cancellationToken),
            SelectedPermissions = string.Equals(user.Role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase)
                ? user.FunctionPermissions.ToList()
                : new List<string>(),
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel model, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.UserManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        if (!ModelState.IsValid)
        {
            await PopulatePermissionGroupsAsync(model, cancellationToken);
            return View(model);
        }

        var result = await _userService.UpdateAsync(new AppUser
        {
            Id = model.Id,
            DisplayName = model.DisplayName.Trim(),
            Email = model.Email.Trim(),
            Role = model.Role,
            PermissionGroupIds = model.SelectedPermissionGroupIds,
            FunctionPermissions = model.SelectedPermissions,
            IsActive = model.IsActive
        }, model.Password, cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            await PopulatePermissionGroupsAsync(model, cancellationToken);
            return View(model);
        }

        TempData["StatusMessage"] = "Cập nhật tài khoản thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, UserFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.UserManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        if (!string.IsNullOrWhiteSpace(id))
        {
            var result = await _userService.DeleteAsync(id, User.Identity?.Name, cancellationToken);
            TempData["StatusMessage"] = result.Success
                ? "Đã xóa tài khoản thành công."
                : result.Error ?? "Không thể xóa tài khoản.";
        }

        return RedirectToAction(nameof(Index), new
        {
            searchTerm = filter.SearchTerm,
            role = filter.Role,
            isActive = filter.IsActive,
            page = filter.Page,
            pageSize = filter.PageSize
        });
    }

    private async Task PopulatePermissionGroupsAsync(UserEditViewModel model, CancellationToken cancellationToken)
    {
        model.AvailablePermissionGroups = await _permissionGroupService.GetActiveAsync(cancellationToken);
    }
}