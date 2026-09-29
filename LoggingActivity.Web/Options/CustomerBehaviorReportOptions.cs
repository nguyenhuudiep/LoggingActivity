namespace LoggingActivity.Web.Options;

public sealed class CustomerBehaviorReportOptions
{
    public const string SectionName = "CustomerBehaviorReport";

    public string Endpoint { get; set; } = "https://apilos.tima.vn/api/v1.0/tool/get_log_call_api";

    public int TimeoutSeconds { get; set; } = 60;

    public int CacheSeconds { get; set; } = 120;

    public int MaxRangeDays { get; set; } = 31;
}
