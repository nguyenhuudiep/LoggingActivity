namespace LoggingActivity.Web.Models;

public sealed class PermissionGroupStatistics
{
    public long TotalGroups { get; init; }

    public long ActiveGroups { get; init; }

    public long InactiveGroups { get; init; }
}