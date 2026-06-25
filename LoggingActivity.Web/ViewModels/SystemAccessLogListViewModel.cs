using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class SystemAccessLogListViewModel
{
    public SystemAccessLogFilterViewModel Filter { get; init; } = new();

    public PagedResult<SystemAccessLog> Logs { get; init; } = new();

    public IReadOnlyList<string> EventTypes { get; init; } = SystemAccessEventTypes.All;
}
