using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin)]
public sealed class PartnersController : AppController
{
    private readonly PartnerService _partnerService;

    public PartnersController(PartnerService partnerService)
    {
        _partnerService = partnerService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] PartnerFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.PartnerManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var partners = await _partnerService.GetPagedAsync(new PartnerQuery
        {
            Page = filter.Page,
            PageSize = filter.PageSize
        }, cancellationToken);

        return View(new PartnerListViewModel
        {
            Filter = filter,
            Partners = partners
        });
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] PartnerFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.PartnerManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var partners = await ReadAllPagesAsync(
            (page, pageSize, token) => _partnerService.GetPagedAsync(new PartnerQuery
            {
                Page = page,
                PageSize = pageSize
            }, token),
            cancellationToken);

        var rows = partners.Select(partner => (IReadOnlyList<string?>)
        [
            partner.Name,
            partner.ApiKey,
            partner.IsActive ? "Active" : "Inactive",
            partner.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss"),
            partner.UpdatedAtUtc.ToString("yyyy-MM-dd HH:mm:ss")
        ]).ToList();

        return BuildCsvFile(
            "partners",
            ["Name", "ApiKey", "Status", "CreatedAtUtc", "UpdatedAtUtc"],
            rows);
    }

    [HttpGet]
    public IActionResult Create()
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.PartnerManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        return View(new PartnerEditViewModel { IsCreateMode = true, IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PartnerEditViewModel model, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.PartnerManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        model.IsCreateMode = true;
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _partnerService.CreateAsync(new Partner
        {
            Name = model.Name,
            IsActive = model.IsActive
        }, cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["StatusMessage"] = "Tạo đối tác thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.PartnerManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var partner = await _partnerService.GetByIdAsync(id, cancellationToken);
        if (partner is null)
        {
            return NotFound();
        }

        return View(new PartnerEditViewModel
        {
            Id = partner.Id,
            Name = partner.Name,
            IsActive = partner.IsActive,
            ApiKey = partner.ApiKey
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PartnerEditViewModel model, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.PartnerManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _partnerService.UpdateAsync(new Partner
        {
            Id = model.Id,
            Name = model.Name,
            IsActive = model.IsActive
        }, cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["StatusMessage"] = "Cập nhật đối tác thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RegenerateApiKey(string partnerId, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.PartnerManagement);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var apiKey = await _partnerService.RegenerateApiKeyAsync(partnerId, cancellationToken);
        if (apiKey is null)
        {
            TempData["StatusMessage"] = "Không thể tạo lại API key cho đối tác này.";
            return RedirectToAction(nameof(Index));
        }

        TempData["StatusMessage"] = $"API key mới: {apiKey}";
        return RedirectToAction(nameof(Index));
    }
}