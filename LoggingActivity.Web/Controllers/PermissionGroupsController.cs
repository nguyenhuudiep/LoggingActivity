using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin)]
public sealed class PermissionGroupsController : AppController
{
    private readonly PermissionGroupService _permissionGroupService;

    public PermissionGroupsController(PermissionGroupService permissionGroupService)
    {
        _permissionGroupService = permissionGroupService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PermissionGroupFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.UserManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var query = new PermissionGroupQuery
        {
            SearchTerm = filter.SearchTerm,
            IsActive = filter.IsActive,
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        return View(new PermissionGroupManagementViewModel
        {
            Filter = filter,
            Groups = await _permissionGroupService.GetPagedAsync(query, cancellationToken),
            Statistics = await _permissionGroupService.GetStatisticsAsync(query, cancellationToken)
        });
    }

    [HttpGet]
    public IActionResult Create()
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.UserManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        return View(new PermissionGroupEditViewModel { IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PermissionGroupEditViewModel model, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.UserManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _permissionGroupService.CreateAsync(new PermissionGroup
        {
            Name = model.Name,
            Description = model.Description,
            FunctionPermissions = model.SelectedPermissions,
            IsActive = model.IsActive
        }, cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["StatusMessage"] = "Tạo nhóm quyền thành công.";
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

        var group = await _permissionGroupService.GetByIdAsync(id, cancellationToken);
        if (group is null)
        {
            return NotFound();
        }

        return View(new PermissionGroupEditViewModel
        {
            Id = group.Id,
            Name = group.Name,
            Description = group.Description,
            SelectedPermissions = group.FunctionPermissions.ToList(),
            IsActive = group.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PermissionGroupEditViewModel model, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.UserManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _permissionGroupService.UpdateAsync(new PermissionGroup
        {
            Id = model.Id,
            Name = model.Name,
            Description = model.Description,
            FunctionPermissions = model.SelectedPermissions,
            IsActive = model.IsActive
        }, cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["StatusMessage"] = "Cập nhật nhóm quyền thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id, PermissionGroupFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.UserManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        if (!string.IsNullOrWhiteSpace(id))
        {
            var result = await _permissionGroupService.DeleteAsync(id, cancellationToken);
            TempData["StatusMessage"] = result.Success
                ? "Đã xóa nhóm quyền thành công."
                : result.Error ?? "Không thể xóa nhóm quyền.";
        }

        return RedirectToAction(nameof(Index), new
        {
            searchTerm = filter.SearchTerm,
            isActive = filter.IsActive,
            page = filter.Page,
            pageSize = filter.PageSize
        });
    }
}