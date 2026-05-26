using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class AlertRuleManagementViewModel
{
    public AlertRuleFilterViewModel Filter { get; init; } = new();

    public AlertRuleInputViewModel Input { get; init; } = new();

    public PagedResult<AlertRule> Rules { get; init; } = new();

    public IReadOnlyList<LogActionDefinition> AvailableActions { get; init; } = Array.Empty<LogActionDefinition>();

    public IReadOnlyList<AlertWarning> ActiveWarnings { get; init; } = Array.Empty<AlertWarning>();
}