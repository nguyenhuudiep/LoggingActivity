namespace LoggingActivity.Web.Models;

public sealed class UserQuery
{
    public string? SearchTerm { get; init; }

    public string? Role { get; init; }

    public bool? IsActive { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}