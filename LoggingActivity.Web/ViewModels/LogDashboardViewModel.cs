using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class LogDashboardViewModel
{
    public LogFilterViewModel Filter { get; init; } = new();

    public PagedResult<ActivityLog> Logs { get; init; } = new();

    public LogStatistics Statistics { get; init; } = new();

    public IReadOnlyList<LogActionDefinition> AvailableActions { get; init; } = Array.Empty<LogActionDefinition>();

    public IReadOnlyList<Partner> AvailablePartners { get; init; } = Array.Empty<Partner>();

    public IReadOnlyList<AlertWarning> ActiveWarnings { get; init; } = Array.Empty<AlertWarning>();

    public IReadOnlyList<UnconfiguredLogActionWarning> UnconfiguredActionWarnings { get; init; } = Array.Empty<UnconfiguredLogActionWarning>();
}