using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class AlertHistoryListViewModel
{
    public AlertHistoryFilterViewModel Filter { get; init; } = new();

    public PagedResult<AlertHistory> Alerts { get; init; } = new();
}