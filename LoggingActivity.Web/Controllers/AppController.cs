using LoggingActivity.Web.Infrastructure;
using LoggingActivity.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

public abstract class AppController : Controller
{
    protected IActionResult? ForbidIfMissingPermission(string permission, bool allowAuditor = false)
    {
        return User.HasFeatureAccess(permission, allowAuditor) ? null : Forbid();
    }

    protected FileContentResult BuildCsvFile(string filePrefix, IReadOnlyList<string> headers, IEnumerable<IReadOnlyList<string?>> rows)
    {
        var fileName = $"{filePrefix}-{DateTime.Now:yyyyMMddHHmmss}.csv";
        var content = CsvExportHelper.Build(headers, rows);
        return File(content, "text/csv; charset=utf-8", fileName);
    }

    protected static async Task<List<T>> ReadAllPagesAsync<T>(
        Func<int, int, CancellationToken, Task<PagedResult<T>>> fetchPageAsync,
        CancellationToken cancellationToken,
        int pageSize = 100)
    {
        var allItems = new List<T>();
        var currentPage = 1;

        while (true)
        {
            var pagedResult = await fetchPageAsync(currentPage, pageSize, cancellationToken);
            if (pagedResult.Items.Count == 0)
            {
                break;
            }

            allItems.AddRange(pagedResult.Items);
            if (currentPage >= pagedResult.TotalPages)
            {
                break;
            }

            currentPage++;
        }

        return allItems;
    }
}