namespace LoggingActivity.Web.Models;

public sealed class PartnerQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 10;
}