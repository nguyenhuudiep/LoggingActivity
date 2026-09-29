using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class CustomerBehaviorFilterViewModel
{
    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int? ActionCall { get; set; }

    public int? ProductId { get; set; }

    public int? Status { get; set; }

    public string? Partner { get; set; }

    // Tên khách hàng hoặc mã hồ sơ (loanBriefId).
    public string? SearchTerm { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 20;
}

public sealed class CustomerBehaviorOption
{
    public string Value { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;
}

public sealed class CustomerBehaviorSeries
{
    public string Label { get; init; } = string.Empty;

    public IReadOnlyList<long> Values { get; init; } = Array.Empty<long>();
}

public sealed class CustomerBehaviorHeatmapRow
{
    public string DayLabel { get; init; } = string.Empty;

    public IReadOnlyList<long> Cells { get; init; } = Array.Empty<long>();
}

public sealed class CustomerBehaviorTopCustomer
{
    public long LoanBriefId { get; init; }

    public string FullName { get; init; } = string.Empty;

    public string ProductName { get; init; } = string.Empty;

    public string StatusName { get; init; } = string.Empty;

    public decimal? LoanAmount { get; init; }

    public long Calls { get; init; }

    public int DistinctActions { get; init; }

    public string TopAction { get; init; } = string.Empty;

    public long TopActionCalls { get; init; }

    public DateTime FirstCallAt { get; init; }

    public DateTime LastCallAt { get; init; }

    public int ActiveDays { get; init; }
}

public sealed class CustomerBehaviorReportViewModel
{
    public CustomerBehaviorFilterViewModel Filter { get; init; } = new();

    public string? ErrorMessage { get; init; }

    public int MaxRangeDays { get; init; }

    public long TotalCalls { get; init; }

    public long SourceTotalCalls { get; init; }

    public int UniqueLoans { get; init; }

    public int UniqueCustomers { get; init; }

    public double AverageCallsPerLoan { get; init; }

    public int DisbursedLoans { get; init; }

    public double DisbursedLoanRate { get; init; }

    public int ReturningLoans { get; init; }

    public string? PeakHourLabel { get; init; }

    public long PeakHourCalls { get; init; }

    public string? BusiestDayLabel { get; init; }

    public long BusiestDayCalls { get; init; }

    public string? TopActionName { get; init; }

    public double TopActionShare { get; init; }

    public IReadOnlyList<CustomerBehaviorOption> ActionOptions { get; init; } = Array.Empty<CustomerBehaviorOption>();

    public IReadOnlyList<CustomerBehaviorOption> ProductOptions { get; init; } = Array.Empty<CustomerBehaviorOption>();

    public IReadOnlyList<CustomerBehaviorOption> StatusOptions { get; init; } = Array.Empty<CustomerBehaviorOption>();

    public IReadOnlyList<CustomerBehaviorOption> PartnerOptions { get; init; } = Array.Empty<CustomerBehaviorOption>();

    public IReadOnlyList<string> DailyLabels { get; init; } = Array.Empty<string>();

    public IReadOnlyList<long> DailyTotals { get; init; } = Array.Empty<long>();

    public IReadOnlyList<long> DailyUniqueLoans { get; init; } = Array.Empty<long>();

    public IReadOnlyList<CustomerBehaviorSeries> DailyActionSeries { get; init; } = Array.Empty<CustomerBehaviorSeries>();

    public IReadOnlyList<long> HourlyTotals { get; init; } = Array.Empty<long>();

    public IReadOnlyList<CustomerBehaviorHeatmapRow> Heatmap { get; init; } = Array.Empty<CustomerBehaviorHeatmapRow>();

    public long HeatmapMax { get; init; }

    public IReadOnlyList<BreakdownItem> ActionBreakdown { get; init; } = Array.Empty<BreakdownItem>();

    public IReadOnlyList<BreakdownItem> ProductBreakdown { get; init; } = Array.Empty<BreakdownItem>();

    public IReadOnlyList<BreakdownItem> StatusBreakdown { get; init; } = Array.Empty<BreakdownItem>();

    public IReadOnlyList<BreakdownItem> PartnerBreakdown { get; init; } = Array.Empty<BreakdownItem>();

    public IReadOnlyList<CustomerBehaviorTopCustomer> TopCustomers { get; init; } = Array.Empty<CustomerBehaviorTopCustomer>();

    public PagedResult<CustomerApiCallLog> Records { get; init; } = new() { Page = 1, PageSize = 20 };
}
