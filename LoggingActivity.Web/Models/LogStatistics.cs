namespace LoggingActivity.Web.Models;

public sealed class LogStatistics
{
    public long TotalLogs { get; init; }

    public long TodayLogs { get; init; }

    public long UniqueUsers { get; init; }

    public long IntegratedLogs { get; init; }

    public IReadOnlyList<ChartPoint> DailyActivity { get; init; } = Array.Empty<ChartPoint>();

    public IReadOnlyList<BreakdownItem> TopActions { get; init; } = Array.Empty<BreakdownItem>();

    public IReadOnlyList<ActionTrendSeries> ActionDailySeries { get; init; } = Array.Empty<ActionTrendSeries>();
}

public sealed class ChartPoint
{
    public string Label { get; init; } = string.Empty;

    public long Value { get; init; }
}

public sealed class BreakdownItem
{
    public string Label { get; init; } = string.Empty;

    public long Value { get; init; }
}

public sealed class ActionTrendSeries
{
    public string Action { get; init; } = string.Empty;

    public IReadOnlyList<long> Values { get; init; } = Array.Empty<long>();
}