using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class HomeDashboardViewModel
{
    public LogStatistics OverviewStatistics { get; init; } = new();

    public IReadOnlyList<AlertWarning> ActiveWarnings { get; init; } = Array.Empty<AlertWarning>();

    public IReadOnlyList<UnconfiguredLogActionWarning> UnconfiguredActionWarnings { get; init; } = Array.Empty<UnconfiguredLogActionWarning>();
}