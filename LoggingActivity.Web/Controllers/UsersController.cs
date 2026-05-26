using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin)]
public sealed class UsersController : Controller
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] UserFilterViewModel filter, CancellationToken cancellationToken)
    {
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
    public IActionResult Create()
    {
        return View(new UserEditViewModel { IsCreateMode = true, IsActive = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserEditViewModel model, CancellationToken cancellationToken)
    {
        model.IsCreateMode = true;
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
            IsActive = user.IsActive
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserEditViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _userService.UpdateAsync(new AppUser
        {
            Id = model.Id,
            DisplayName = model.DisplayName.Trim(),
            Email = model.Email.Trim(),
            Role = model.Role,
            IsActive = model.IsActive
        }, model.Password, cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Error!);
            return View(model);
        }

        TempData["StatusMessage"] = "Cập nhật tài khoản thành công.";
        return RedirectToAction(nameof(Index));
    }
}