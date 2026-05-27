namespace LoggingActivity.Web.Models;

public sealed class ActivityLogIngestQueueQuery
{
    public string? PartnerId { get; init; }

    public string? Status { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}