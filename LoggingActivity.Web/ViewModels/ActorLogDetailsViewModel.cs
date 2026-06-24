using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class ActorLogDetailsViewModel
{
    public ActorLogDetailsFilterViewModel Filter { get; init; } = new();

    public PagedResult<ActivityLog> Logs { get; init; } = new();

    public IReadOnlyList<LogActionDefinition> AvailableActions { get; init; } = Array.Empty<LogActionDefinition>();

    public string ActorIdentifier { get; init; } = string.Empty;

    public string ActorIdentifierType { get; init; } = ActorIdentifierTypes.Unknown;

    public string ActorLabel { get; init; } = "Key";
}