using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class AlertHistoryDetailsViewModel
{
    public AlertHistoryDetailsFilterViewModel Filter { get; set; } = new();

    public PagedResult<ActivityLog> Logs { get; set; } = new();

    public string ActorIdentifier { get; set; } = string.Empty;

    public string ActorIdentifierType { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public DateTime AlertDate { get; set; }

    public string ActorLabel { get; set; } = string.Empty;
}
