namespace LoggingActivity.Web.Models;

public sealed class SystemAccessLogQuery
{
    public string? SearchTerm { get; init; }

    public string? EventType { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}
