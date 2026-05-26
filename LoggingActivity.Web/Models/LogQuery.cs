namespace LoggingActivity.Web.Models;

public sealed class LogQuery
{
    public string? SearchTerm { get; init; }

    public string? PartnerId { get; init; }

    public string? Action { get; init; }

    public string? Source { get; init; }

    public DateTime? FromUtc { get; init; }

    public DateTime? ToUtc { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}