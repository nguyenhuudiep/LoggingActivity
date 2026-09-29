using System.Globalization;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[Authorize(Roles = SystemRoles.Admin + "," + SystemRoles.Auditor)]
public sealed class CustomerBehaviorController : AppController
{
    private readonly CustomerBehaviorReportService _reportService;

    public CustomerBehaviorController(CustomerBehaviorReportService reportService)
    {
        _reportService = reportService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] CustomerBehaviorFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.CustomerBehaviorReport, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        _reportService.NormalizeFilter(filter);
        return View(await _reportService.BuildReportAsync(filter, cancellationToken));
    }

    [HttpGet]
    public async Task<IActionResult> Export([FromQuery] CustomerBehaviorFilterViewModel filter, CancellationToken cancellationToken)
    {
        var accessDenied = ForbidIfMissingPermission(AdminFunctionPermissions.CustomerBehaviorReport, allowAuditor: true);
        if (accessDenied is not null)
        {
            return accessDenied;
        }

        _reportService.NormalizeFilter(filter);

        IReadOnlyList<CustomerApiCallLog> logs;
        try
        {
            logs = await _reportService.GetFilteredLogsAsync(filter, cancellationToken);
        }
        catch (CustomerBehaviorApiException)
        {
            // Trang báo cáo tự hiển thị lỗi API cho cùng bộ lọc.
            return RedirectToAction(nameof(Index), new
            {
                from = filter.From?.ToString("yyyy-MM-dd"),
                to = filter.To?.ToString("yyyy-MM-dd"),
                actionCall = filter.ActionCall,
                productId = filter.ProductId,
                status = filter.Status,
                partner = filter.Partner,
                searchTerm = filter.SearchTerm
            });
        }

        var rows = logs.Select(log => (IReadOnlyList<string?>)
        [
            log.CreatedTimeLog.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            log.LoanBriefId.ToString(CultureInfo.InvariantCulture),
            log.FullName,
            log.ActionCall.ToString(CultureInfo.InvariantCulture),
            log.ActionCallName,
            log.StatusName,
            log.ProductName,
            log.LoanAmount?.ToString("0", CultureInfo.InvariantCulture),
            log.LoanTime?.ToString(CultureInfo.InvariantCulture),
            log.RateTypeName,
            log.DisbursementPartner,
            log.CreatedTime?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            log.DisbursementAt?.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
        ]);

        return BuildCsvFile(
            "customer-behavior",
            [
                "Thời gian gọi", "Mã HĐ", "Khách hàng", "Mã action", "Action", "Trạng thái", "Sản phẩm",
                "Số tiền vay", "Thời hạn (tháng)", "Hình thức lãi", "Đối tác giải ngân", "Ngày tạo đơn", "Ngày giải ngân"
            ],
            rows);
    }
}
