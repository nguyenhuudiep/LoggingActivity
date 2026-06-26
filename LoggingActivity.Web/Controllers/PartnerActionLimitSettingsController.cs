using LoggingActivity.Web.Contracts;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin)]
public sealed class PartnerActionLimitSettingsController : AppController
{
    private readonly PartnerService _partnerService;
    private readonly PartnerUserActionLimitService _partnerUserActionLimitService;
    private readonly LogActionDefinitionService _logActionDefinitionService;

    public PartnerActionLimitSettingsController(
        PartnerService partnerService,
        PartnerUserActionLimitService partnerUserActionLimitService,
        LogActionDefinitionService logActionDefinitionService)
    {
        _partnerService = partnerService;
        _partnerUserActionLimitService = partnerUserActionLimitService;
        _logActionDefinitionService = logActionDefinitionService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PartnerActionLimitFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.PartnerManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var normalizedFilter = NormalizeFilter(filter);
        var viewModel = await BuildViewModelAsync(normalizedFilter, cancellationToken);
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Upsert(
        PartnerUserActionLimitUpsertRequest request,
        [FromForm] string? filterPartnerId,
        [FromForm] string? filterUserId,
        [FromForm] string? filterUserKeyType,
        [FromForm] string? filterAction,
        [FromForm] bool? filterIsActive,
        [FromForm] int filterPage,
        [FromForm] int filterPageSize,
        CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.PartnerManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var filter = NormalizeFilter(new PartnerActionLimitFilterViewModel
        {
            PartnerId = filterPartnerId,
            UserId = filterUserId,
            UserKeyType = filterUserKeyType,
            Action = filterAction,
            IsActive = filterIsActive,
            Page = filterPage,
            PageSize = filterPageSize
        });

        request.UserKeyType = NormalizeUserKeyType(request.UserKeyType);
        request.Action = string.IsNullOrWhiteSpace(request.Action)
            ? string.Empty
            : request.Action.Trim().ToUpperInvariant();

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildViewModelAsync(filter, cancellationToken);
            invalidModel.UpsertRequest = request;
            return View("Index", invalidModel);
        }

        await _partnerUserActionLimitService.UpsertAsync(
            request.PartnerId,
            request.UserId,
            request.UserKeyType,
            request.Action,
            request.DailyLimit,
            request.IsActive,
            cancellationToken);

        TempData["StatusMessage"] = "Đã lưu cấu hình hạn mức user-action.";

        if (string.IsNullOrWhiteSpace(filter.PartnerId))
        {
            filter.PartnerId = request.PartnerId;
        }

        filter.Page = 1;
        return RedirectToAction(nameof(Index), BuildFilterRouteValues(filter));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(
        PartnerUserActionLimitDeleteRequest request,
        [FromForm] string? filterPartnerId,
        [FromForm] string? filterUserId,
        [FromForm] string? filterUserKeyType,
        [FromForm] string? filterAction,
        [FromForm] bool? filterIsActive,
        [FromForm] int filterPage,
        [FromForm] int filterPageSize,
        CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.PartnerManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var filter = NormalizeFilter(new PartnerActionLimitFilterViewModel
        {
            PartnerId = filterPartnerId,
            UserId = filterUserId,
            UserKeyType = filterUserKeyType,
            Action = filterAction,
            IsActive = filterIsActive,
            Page = filterPage,
            PageSize = filterPageSize
        });

        request.Action = string.IsNullOrWhiteSpace(request.Action)
            ? string.Empty
            : request.Action.Trim().ToUpperInvariant();

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildViewModelAsync(filter, cancellationToken);
            return View("Index", invalidModel);
        }

        await _partnerUserActionLimitService.DeleteAsync(request.PartnerId, request.UserId, request.Action, cancellationToken);
        TempData["StatusMessage"] = "Đã xóa cấu hình hạn mức user-action.";
        return RedirectToAction(nameof(Index), BuildFilterRouteValues(filter));
    }

    private async Task<PartnerActionLimitSettingsViewModel> BuildViewModelAsync(PartnerActionLimitFilterViewModel filter, CancellationToken cancellationToken)
    {
        var availableActions = await _logActionDefinitionService.GetAllAsync(cancellationToken);
        var allRules = await _partnerUserActionLimitService.GetAllAsync(cancellationToken);

        var partners = await ReadAllPagesAsync(
            (page, pageSize, token) => _partnerService.GetPagedAsync(new PartnerQuery
            {
                Page = page,
                PageSize = pageSize
            }, token),
            cancellationToken);

        var partnerOptions = partners
            .Select(item => new PartnerOptionViewModel
            {
                Id = item.Id ?? string.Empty,
                Name = item.Name,
                IsActive = item.IsActive
            })
            .Where(item => !string.IsNullOrWhiteSpace(item.Id))
            .OrderBy(item => item.Name)
            .ToList();

        var partnerLookup = partnerOptions.ToDictionary(item => item.Id, item => item.Name, StringComparer.OrdinalIgnoreCase);

        var userKeyTypeOptions = new[]
        {
            new SelectOptionViewModel { Value = ActorIdentifierTypes.UserId, Label = "User ID" },
            new SelectOptionViewModel { Value = ActorIdentifierTypes.Phone, Label = "Số điện thoại" },
            new SelectOptionViewModel { Value = ActorIdentifierTypes.InternalUserId, Label = "Mã tài khoản nội bộ" }
        };

        var actionOptions = availableActions
            .OrderBy(item => item.Code)
            .Select(item => new SelectOptionViewModel
            {
                Value = item.Code,
                Label = string.IsNullOrWhiteSpace(item.DisplayName)
                    ? item.Code
                    : $"{item.Code} - {item.DisplayName}"
            })
            .ToList();

        var normalizedFilter = NormalizeFilter(filter);

        IEnumerable<PartnerActionLimitListItemViewModel> filteredQuery = allRules.Select(rule => new PartnerActionLimitListItemViewModel
        {
            PartnerId = rule.PartnerId,
            PartnerName = partnerLookup.TryGetValue(rule.PartnerId, out var partnerName) ? partnerName : rule.PartnerId,
            ActorIdentifier = rule.ActorIdentifier,
            ActorIdentifierType = rule.ActorIdentifierType,
            Action = rule.Action,
            DailyLimit = rule.DailyLimit,
            IsActive = rule.IsActive
        });

        if (!string.IsNullOrWhiteSpace(normalizedFilter.PartnerId))
        {
            filteredQuery = filteredQuery.Where(item => string.Equals(item.PartnerId, normalizedFilter.PartnerId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(normalizedFilter.UserId))
        {
            filteredQuery = filteredQuery.Where(item => item.ActorIdentifier.Contains(normalizedFilter.UserId, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(normalizedFilter.UserKeyType))
        {
            filteredQuery = filteredQuery.Where(item => string.Equals(item.ActorIdentifierType, normalizedFilter.UserKeyType, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(normalizedFilter.Action))
        {
            filteredQuery = filteredQuery.Where(item => string.Equals(item.Action, normalizedFilter.Action, StringComparison.OrdinalIgnoreCase));
        }

        if (normalizedFilter.IsActive.HasValue)
        {
            filteredQuery = filteredQuery.Where(item => item.IsActive == normalizedFilter.IsActive.Value);
        }

        var orderedItems = filteredQuery
            .OrderBy(item => item.PartnerName)
            .ThenBy(item => item.ActorIdentifier)
            .ThenBy(item => item.Action)
            .ToList();

        var totalCount = orderedItems.Count;
        var pagedItems = orderedItems
            .Skip((normalizedFilter.Page - 1) * normalizedFilter.PageSize)
            .Take(normalizedFilter.PageSize)
            .ToList();

        return new PartnerActionLimitSettingsViewModel
        {
            Filter = normalizedFilter,
            PartnerOptions = partnerOptions,
            UserKeyTypeOptions = userKeyTypeOptions,
            ActionOptions = actionOptions,
            Rules = new PagedResult<PartnerActionLimitListItemViewModel>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                Page = normalizedFilter.Page,
                PageSize = normalizedFilter.PageSize
            },
            UpsertRequest = new PartnerUserActionLimitUpsertRequest
            {
                PartnerId = !string.IsNullOrWhiteSpace(normalizedFilter.PartnerId)
                    ? normalizedFilter.PartnerId!
                    : partnerOptions.FirstOrDefault()?.Id ?? string.Empty,
                Action = normalizedFilter.Action ?? string.Empty,
                UserKeyType = ActorIdentifierTypes.UserId,
                IsActive = true
            }
        };
    }

    private static PartnerActionLimitFilterViewModel NormalizeFilter(PartnerActionLimitFilterViewModel filter)
    {
        var normalizedUserKeyType = string.IsNullOrWhiteSpace(filter.UserKeyType)
            ? null
            : ActorIdentityHelper.NormalizeType(filter.UserKeyType, null);

        if (string.Equals(normalizedUserKeyType, ActorIdentifierTypes.Unknown, StringComparison.OrdinalIgnoreCase))
        {
            normalizedUserKeyType = null;
        }

        return new PartnerActionLimitFilterViewModel
        {
            PartnerId = string.IsNullOrWhiteSpace(filter.PartnerId) ? null : filter.PartnerId.Trim(),
            UserId = string.IsNullOrWhiteSpace(filter.UserId) ? null : filter.UserId.Trim(),
            UserKeyType = normalizedUserKeyType,
            Action = string.IsNullOrWhiteSpace(filter.Action) ? null : filter.Action.Trim().ToUpperInvariant(),
            IsActive = filter.IsActive,
            Page = filter.Page <= 0 ? 1 : filter.Page,
            PageSize = filter.PageSize is < 1 or > 200 ? 10 : filter.PageSize
        };
    }

    private static object BuildFilterRouteValues(PartnerActionLimitFilterViewModel filter)
    {
        return new
        {
            partnerId = filter.PartnerId,
            userId = filter.UserId,
            userKeyType = filter.UserKeyType,
            action = filter.Action,
            isActive = filter.IsActive,
            page = filter.Page,
            pageSize = filter.PageSize
        };
    }

    private static string NormalizeUserKeyType(string? userKeyType)
    {
        var normalizedType = ActorIdentityHelper.NormalizeType(userKeyType, null);
        return string.Equals(normalizedType, ActorIdentifierTypes.Unknown, StringComparison.OrdinalIgnoreCase)
            ? ActorIdentifierTypes.UserId
            : normalizedType;
    }
}
