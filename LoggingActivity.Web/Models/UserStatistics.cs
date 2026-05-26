namespace LoggingActivity.Web.Models;

public sealed class UserStatistics
{
    public long TotalUsers { get; init; }

    public long ActiveUsers { get; init; }

    public long InactiveUsers { get; init; }
}