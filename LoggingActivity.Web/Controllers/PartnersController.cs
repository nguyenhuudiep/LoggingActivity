using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin)]
public sealed class PartnersController : Controller
{
    private readonly PartnerService _partnerService;

    public PartnersController(PartnerService partnerService)
    {
        _partnerService = partnerService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var partners = await _partnerService.GetAllAsync(cancellationToken);
        return View(partners);
    }

    [HttpGet]
    public IActionResult Create()
    {
        return View(new PartnerEditViewModel { IsCreateMode = true, IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PartnerEditViewModel model, CancellationToken cancellationToken)
    {
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