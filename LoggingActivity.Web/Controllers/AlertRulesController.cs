using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin)]
public sealed class AlertRulesController : AppController
{
    private readonly AlertRuleService _alertRuleService;
    private readonly LogActionDefinitionService _logActionDefinitionService;

    public AlertRulesController(AlertRuleService alertRuleService, LogActionDefinitionService logActionDefinitionService)
    {
        _alertRuleService = alertRuleService;
        _logActionDefinitionService = logActionDefinitionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AlertRuleFilterViewModel filter, string? editAction, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.AlertRuleManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        return View(await BuildViewModelAsync(filter, null, editAction, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save([Bind(Prefix = "Input")] AlertRuleInputViewModel model, AlertRuleFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.AlertRuleManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        if (!ModelState.IsValid)
        {
            return View(nameof(Index), await BuildViewModelAsync(filter, model, model.ExistingAction, cancellationToken));
        }

        var result = await _alertRuleService.UpsertAsync(model.ExistingAction, model.Action, model.DailyLimit, model.IsActive, cancellationToken);
        if (!result.Success)
        {
            ModelState.AddModelError(nameof(model.Action), result.Error!);
            return View(nameof(Index), await BuildViewModelAsync(filter, model, model.ExistingAction, cancellationToken));
        }

        TempData["StatusMessage"] = string.IsNullOrWhiteSpace(model.ExistingAction)
            ? $"Đã lưu cấu hình cảnh báo cho action '{model.Action.Trim()}'."
            : $"Đã cập nhật cấu hình cảnh báo cho action '{model.Action.Trim()}'.";

        return RedirectToAction(nameof(Index), new
        {
            searchTerm = filter.SearchTerm,
            actionCode = filter.ActionCode,
            isActive = filter.IsActive,
            page = filter.Page,
            pageSize = filter.PageSize
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string action, AlertRuleFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.AlertRuleManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            await _alertRuleService.DeleteAsync(action, cancellationToken);
            TempData["StatusMessage"] = "Đã xóa cấu hình cảnh báo.";
        }

        return RedirectToAction(nameof(Index), new
        {
            searchTerm = filter.SearchTerm,
            actionCode = filter.ActionCode,
            isActive = filter.IsActive,
            page = filter.Page,
            pageSize = filter.PageSize
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleStatus(string action, AlertRuleFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.AlertRuleManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        if (!string.IsNullOrWhiteSpace(action))
        {
            var success = await _alertRuleService.ToggleStatusAsync(action, cancellationToken);
            TempData["StatusMessage"] = success
                ? "Đã cập nhật trạng thái cảnh báo."
                : "Không tìm thấy cấu hình cảnh báo cần cập nhật trạng thái.";
        }

        return RedirectToAction(nameof(Index), new
        {
            searchTerm = filter.SearchTerm,
            actionCode = filter.ActionCode,
            isActive = filter.IsActive,
            page = filter.Page,
            pageSize = filter.PageSize
        });
    }

    private async Task<AlertRuleManagementViewModel> BuildViewModelAsync(
        AlertRuleFilterViewModel filter,
        AlertRuleInputViewModel? input,
        string? editAction,
        CancellationToken cancellationToken)
    {
        var query = new AlertRuleQuery
        {
            SearchTerm = filter.SearchTerm,
            ActionCode = filter.ActionCode,
            IsActive = filter.IsActive,
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        if (input is null && !string.IsNullOrWhiteSpace(editAction))
        {
            var editingRule = await _alertRuleService.GetByActionAsync(editAction, cancellationToken);
            if (editingRule is not null)
            {
                input = new AlertRuleInputViewModel
                {
                    ExistingAction = editingRule.Action,
                    Action = editingRule.Action,
                    DailyLimit = editingRule.DailyLimit,
                    IsActive = editingRule.IsActive
                };
            }
        }

        return new AlertRuleManagementViewModel
        {
            Filter = filter,
            Input = input ?? new AlertRuleInputViewModel(),
            Rules = await _alertRuleService.GetPagedAsync(query, cancellationToken),
            AvailableActions = await _logActionDefinitionService.GetActiveAsync(cancellationToken)
        };
    }
}