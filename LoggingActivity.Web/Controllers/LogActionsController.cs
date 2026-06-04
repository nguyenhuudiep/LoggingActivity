using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin)]
public sealed class LogActionsController : AppController
{
    private readonly LogActionDefinitionService _logActionDefinitionService;
    private readonly AlertRuleService _alertRuleService;

    public LogActionsController(LogActionDefinitionService logActionDefinitionService, AlertRuleService alertRuleService)
    {
        _logActionDefinitionService = logActionDefinitionService;
        _alertRuleService = alertRuleService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] LogActionFilterViewModel filter, string? editCode, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogActionManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        return View(await BuildViewModelAsync(filter, null, editCode, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] LogActionFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogActionManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var actions = await ReadAllPagesAsync(
            (page, pageSize, token) => _logActionDefinitionService.GetPagedAsync(new LogActionQuery
            {
                SearchTerm = filter.SearchTerm,
                IsActive = filter.IsActive,
                Page = page,
                PageSize = pageSize
            }, token),
            cancellationToken);

        var rows = actions.Select(action => (IReadOnlyList<string?>)
        [
            action.Code,
            action.DisplayName,
            action.Description,
            action.IsActive ? "Active" : "Paused",
            action.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
            action.UpdatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss")
        ]).ToList();

        return BuildCsvFile(
            "log-actions",
            ["Code", "DisplayName", "Description", "Status", "CreatedAtUtc", "UpdatedAtUtc"],
            rows);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([Bind(Prefix = "Input")] LogActionDefinitionInputViewModel model, [FromQuery] LogActionFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogActionManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildViewModelAsync(filter, model, model.ExistingCode, cancellationToken));
        }

        var result = await _logActionDefinitionService.UpsertAsync(model.ExistingCode, model.Code, model.DisplayName, model.Description, model.IsActive, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(nameof(model.Code), result.Error!);
            return View(nameof(Index), await BuildViewModelAsync(filter, model, model.ExistingCode, cancellationToken));
        }

        TempData["StatusMessage"] = string.IsNullOrWhiteSpace(model.ExistingCode)
            ? $"Đã tạo action '{model.Code.Trim()}'."
            : $"Đã cập nhật action '{model.Code.Trim()}'.";
        return RedirectToAction(nameof(Index), new
        {
            searchTerm = filter.SearchTerm,
            isActive = filter.IsActive,
            page = filter.Page,
            pageSize = filter.PageSize
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string code, [FromQuery] LogActionFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogActionManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            await _logActionDefinitionService.DeleteAsync(code, cancellationToken);
            TempData["StatusMessage"] = "Đã xóa action log.";
        }

        return RedirectToAction(nameof(Index), new
        {
            searchTerm = filter.SearchTerm,
            isActive = filter.IsActive,
            page = filter.Page,
            pageSize = filter.PageSize
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string code, [FromQuery] LogActionFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogActionManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        if (!string.IsNullOrWhiteSpace(code))
        {
            var success = await _logActionDefinitionService.ToggleStatusAsync(code, cancellationToken);
            TempData["StatusMessage"] = success
                ? "Đã cập nhật trạng thái action log."
                : "Không tìm thấy action log cần cập nhật trạng thái.";
        }

        return RedirectToAction(nameof(Index), new
        {
            searchTerm = filter.SearchTerm,
            isActive = filter.IsActive,
            page = filter.Page,
            pageSize = filter.PageSize
        });
    }

    private async Task<LogActionManagementViewModel> BuildViewModelAsync(
        LogActionFilterViewModel filter,
        LogActionDefinitionInputViewModel? input,
        string? editCode,
        CancellationToken cancellationToken)
    {
        var query = new LogActionQuery
        {
            SearchTerm = filter.SearchTerm,
            IsActive = filter.IsActive,
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        if (input is null && !string.IsNullOrWhiteSpace(editCode))
        {
            var editingAction = await _logActionDefinitionService.GetByCodeAsync(editCode, cancellationToken);
            if (editingAction is not null)
            {
                input = new LogActionDefinitionInputViewModel
                {
                    ExistingCode = editingAction.Code,
                    Code = editingAction.Code,
                    DisplayName = editingAction.DisplayName,
                    Description = editingAction.Description,
                    IsActive = editingAction.IsActive
                };
            }
        }

        return new LogActionManagementViewModel
        {
            Filter = filter,
            Input = input ?? new LogActionDefinitionInputViewModel(),
            Actions = await _logActionDefinitionService.GetPagedAsync(query, cancellationToken),
            UnconfiguredActionWarnings = await _alertRuleService.GetUnconfiguredActionWarningsAsync(cancellationToken)
        };
    }
}