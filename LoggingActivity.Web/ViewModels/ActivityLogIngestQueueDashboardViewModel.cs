using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class ActivityLogIngestQueueDashboardViewModel
{
    public ActivityLogIngestQueueFilterViewModel Filter { get; init; } = new();

    public PagedResult<ActivityLogIngestQueueItem> Items { get; init; } = new();

    public ActivityLogIngestQueueSummary Summary { get; init; } = new();

    public IReadOnlyList<Partner> AvailablePartners { get; init; } = Array.Empty<Partner>();

    public IReadOnlyList<string> AvailableStatuses { get; init; } = new[]
    {
        ActivityLogIngestQueueStatuses.Pending,
        ActivityLogIngestQueueStatuses.Processing,
        ActivityLogIngestQueueStatuses.Failed,
        ActivityLogIngestQueueStatuses.Completed
    };
}