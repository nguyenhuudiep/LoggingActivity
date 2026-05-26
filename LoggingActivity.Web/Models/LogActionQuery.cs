namespace LoggingActivity.Web.Models;

public sealed class LogActionQuery
{
    public string? SearchTerm { get; init; }

    public bool? IsActive { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}