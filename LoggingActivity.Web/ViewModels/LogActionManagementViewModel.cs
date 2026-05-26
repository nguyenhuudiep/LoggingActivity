using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class LogActionManagementViewModel
{
    public LogActionFilterViewModel Filter { get; init; } = new();

    public LogActionDefinitionInputViewModel Input { get; init; } = new();

    public PagedResult<LogActionDefinition> Actions { get; init; } = new();

    public IReadOnlyList<UnconfiguredLogActionWarning> UnconfiguredActionWarnings { get; init; } = Array.Empty<UnconfiguredLogActionWarning>();
}