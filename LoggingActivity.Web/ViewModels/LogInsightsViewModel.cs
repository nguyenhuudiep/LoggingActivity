using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class LogInsightsViewModel
{
    public LogStatistics Statistics { get; init; } = new();

    public IReadOnlyList<AlertWarning> ActiveWarnings { get; init; } = Array.Empty<AlertWarning>();

    public IReadOnlyList<UnconfiguredLogActionWarning> UnconfiguredActionWarnings { get; init; } = Array.Empty<UnconfiguredLogActionWarning>();
}
