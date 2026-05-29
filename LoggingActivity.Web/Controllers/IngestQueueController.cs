using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin + "," + SystemRoles.Auditor)]
public sealed class IngestQueueController : AppController
{
    private readonly ActivityLogIngestQueueService _queueService;
    private readonly PartnerService _partnerService;

    public IngestQueueController(
        ActivityLogIngestQueueService queueService,
        PartnerService partnerService)
    {
        _queueService = queueService;
        _partnerService = partnerService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ActivityLogIngestQueueFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogDashboard, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        filter.Status = string.IsNullOrWhiteSpace(filter.Status) || string.Equals(filter.Status, "All", StringComparison.OrdinalIgnoreCase)
            ? null
            : filter.Status.Trim();
        filter.From ??= DateTime.Today.AddDays(-3);
        filter.To ??= DateTime.Today;

        var query = new ActivityLogIngestQueueQuery
        {
            PartnerId = filter.PartnerId,
            Status = filter.Status,
            FromUtc = filter.From?.Date,
            ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        return View(new ActivityLogIngestQueueDashboardViewModel
        {
            Filter = filter,
            Items = await _queueService.GetPagedAsync(query, cancellationToken),
            Summary = await _queueService.GetSummaryAsync(query, cancellationToken),
            AvailablePartners = await _partnerService.GetAllAsync(cancellationToken)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SeedDemo([FromForm] ActivityLogIngestQueueFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogDashboard);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var partners = await _partnerService.GetAllAsync(cancellationToken);
        var selectedPartner = !string.IsNullOrWhiteSpace(filter.PartnerId)
            ? partners.FirstOrDefault(item => item.Id == filter.PartnerId)
            : partners.FirstOrDefault();

        await _queueService.SeedDemoStatesAsync(selectedPartner, cancellationToken);
        TempData["StatusMessage"] = "Đã tạo 2 bản ghi demo cho hàng đợi ingest: Pending và Failed.";

        return RedirectToAction(nameof(Index), new
        {
            partnerId = filter.PartnerId,
            status = filter.Status,
            from = filter.From?.ToString("dd/MM/yyyy"),
            to = filter.To?.ToString("dd/MM/yyyy"),
            page = filter.Page,
            pageSize = filter.PageSize
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDemo([FromForm] ActivityLogIngestQueueFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogDashboard);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var deletedCount = await _queueService.DeleteDemoStatesAsync(cancellationToken);
        TempData["StatusMessage"] = deletedCount > 0
            ? $"Đã xóa {deletedCount} bản ghi demo khỏi hàng đợi ingest."
            : "Không có bản ghi demo nào để xóa khỏi hàng đợi ingest.";

        return RedirectToAction(nameof(Index), new
        {
            partnerId = filter.PartnerId,
            status = filter.Status,
            from = filter.From?.ToString("dd/MM/yyyy"),
            to = filter.To?.ToString("dd/MM/yyyy"),
            page = filter.Page,
            pageSize = filter.PageSize
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryFailed([FromForm] string id, [FromForm] ActivityLogIngestQueueFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogDashboard);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        var retried = await _queueService.RetryFailedAsync(id, cancellationToken);
        TempData["StatusMessage"] = retried
            ? "Đã đưa request thất bại trở lại hàng đợi để xử lý lại ngay."
            : "Không thể retry request này. Bản ghi có thể không còn ở trạng thái Failed nữa.";

        return RedirectToAction(nameof(Index), new
        {
            partnerId = filter.PartnerId,
            status = filter.Status,
            from = filter.From?.ToString("dd/MM/yyyy"),
            to = filter.To?.ToString("dd/MM/yyyy"),
            page = filter.Page,
            pageSize = filter.PageSize
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryFailedBulk([FromForm] ActivityLogIngestQueueFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.LogDashboard);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        filter.Status = string.IsNullOrWhiteSpace(filter.Status) || string.Equals(filter.Status, "All", StringComparison.OrdinalIgnoreCase)
            ? null
            : filter.Status.Trim();

        var query = new ActivityLogIngestQueueQuery
        {
            PartnerId = filter.PartnerId,
            Status = filter.Status,
            FromUtc = filter.From?.Date,
            ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        var retriedCount = await _queueService.RetryFailedAsync(query, cancellationToken);
        TempData["StatusMessage"] = retriedCount > 0
            ? $"Đã đưa {retriedCount} request thất bại trở lại hàng đợi để xử lý lại ngay."
            : "Không có request thất bại nào phù hợp bộ lọc để retry.";

        return RedirectToAction(nameof(Index), new
        {
            partnerId = filter.PartnerId,
            status = filter.Status,
            from = filter.From?.ToString("dd/MM/yyyy"),
            to = filter.To?.ToString("dd/MM/yyyy"),
            page = filter.Page,
            pageSize = filter.PageSize
        });
    }
}