using System.Globalization;
using LoggingActivity.Web.Infrastructure;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Options;
using LoggingActivity.Web.ViewModels;
using Microsoft.Extensions.Options;

namespace LoggingActivity.Web.Services;

public sealed class CustomerBehaviorReportService
{
    private const int TopActionSeriesCount = 5;
    private const int BreakdownLimit = 8;
    private const int TopCustomerLimit = 10;
    private const string OtherLabel = "Khác";
    private const string UnknownLabel = "Không xác định";
    private const int DisbursedStatus = 100;

    private static readonly string[] WeekdayLabels = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

    private readonly CustomerBehaviorApiClient _apiClient;
    private readonly IOptions<CustomerBehaviorReportOptions> _options;

    public CustomerBehaviorReportService(CustomerBehaviorApiClient apiClient, IOptions<CustomerBehaviorReportOptions> options)
    {
        _apiClient = apiClient;
        _options = options;
    }

    public int MaxRangeDays => Math.Max(_options.Value.MaxRangeDays, 1);

    // Chuẩn hóa khoảng ngày: mặc định 7 ngày gần nhất theo giờ Việt Nam, đảo nếu nhập ngược, giới hạn độ dài.
    public void NormalizeFilter(CustomerBehaviorFilterViewModel filter)
    {
        var today = VietnamTimeExtensions.TodayInVietnamDate();
        var to = (filter.To ?? today).Date;
        var from = (filter.From ?? to.AddDays(-6)).Date;
        if (from > to)
        {
            (from, to) = (to, from);
        }

        if ((to - from).TotalDays + 1 > MaxRangeDays)
        {
            from = to.AddDays(-(MaxRangeDays - 1));
        }

        filter.From = from;
        filter.To = to;
        filter.Page = Math.Clamp(filter.Page, 1, 100_000);
        filter.PageSize = filter.PageSize is 10 or 20 or 50 or 100 ? filter.PageSize : 20;
        filter.SearchTerm = string.IsNullOrWhiteSpace(filter.SearchTerm) ? null : filter.SearchTerm.Trim();
        filter.Partner = string.IsNullOrWhiteSpace(filter.Partner) ? null : filter.Partner.Trim();
    }

    public async Task<IReadOnlyList<CustomerApiCallLog>> GetFilteredLogsAsync(CustomerBehaviorFilterViewModel filter, CancellationToken cancellationToken)
    {
        var logs = await _apiClient.GetLogsAsync(filter.From!.Value, filter.To!.Value, cancellationToken);
        return ApplyFilter(logs, filter)
            .OrderByDescending(log => log.CreatedTimeLog)
            .ToList();
    }

    public async Task<CustomerBehaviorReportViewModel> BuildReportAsync(CustomerBehaviorFilterViewModel filter, CancellationToken cancellationToken)
    {
        IReadOnlyList<CustomerApiCallLog> source;
        try
        {
            source = await _apiClient.GetLogsAsync(filter.From!.Value, filter.To!.Value, cancellationToken);
        }
        catch (CustomerBehaviorApiException ex)
        {
            return new CustomerBehaviorReportViewModel
            {
                Filter = filter,
                ErrorMessage = ex.Message,
                MaxRangeDays = MaxRangeDays,
                Records = new PagedResult<CustomerApiCallLog> { Page = 1, PageSize = filter.PageSize }
            };
        }

        var logs = ApplyFilter(source, filter).ToList();
        var days = Enumerable.Range(0, (int)(filter.To!.Value - filter.From!.Value).TotalDays + 1)
            .Select(offset => filter.From.Value.AddDays(offset))
            .ToList();

        var loans = logs.GroupBy(log => log.LoanBriefId).ToList();
        var uniqueLoans = loans.Count;
        var disbursedLoans = loans.Count(group => group.Any(log => log.Status == DisbursedStatus));
        var returningLoans = loans.Count(group => group.Select(log => log.CreatedTimeLog.Date).Distinct().Count() > 1);

        var hourlyTotals = new long[24];
        var heatmap = new long[7, 24];
        foreach (var log in logs)
        {
            var hour = log.CreatedTimeLog.Hour;
            hourlyTotals[hour]++;
            heatmap[ToMondayFirstIndex(log.CreatedTimeLog.DayOfWeek), hour]++;
        }

        var callsByDay = logs.GroupBy(log => log.CreatedTimeLog.Date).ToDictionary(group => group.Key, group => group.ToList());
        var dailyTotals = days.Select(day => callsByDay.TryGetValue(day, out var items) ? (long)items.Count : 0).ToList();
        var dailyUniqueLoans = days
            .Select(day => callsByDay.TryGetValue(day, out var items) ? (long)items.Select(log => log.LoanBriefId).Distinct().Count() : 0)
            .ToList();

        var actionBreakdown = BuildBreakdown(logs, log => ActionLabel(log), BreakdownLimit);
        var topActionNames = logs
            .GroupBy(ActionLabel)
            .OrderByDescending(group => group.Count())
            .Take(TopActionSeriesCount)
            .Select(group => group.Key)
            .ToList();

        var dailyActionSeries = topActionNames
            .Select(action => new CustomerBehaviorSeries
            {
                Label = action,
                Values = days.Select(day => callsByDay.TryGetValue(day, out var items)
                    ? (long)items.Count(log => ActionLabel(log) == action)
                    : 0).ToList()
            })
            .ToList();

        var otherValues = days
            .Select((day, index) => dailyTotals[index] - dailyActionSeries.Sum(series => series.Values[index]))
            .ToList();
        if (otherValues.Any(value => value > 0))
        {
            dailyActionSeries.Add(new CustomerBehaviorSeries { Label = OtherLabel, Values = otherValues });
        }

        var peakHour = Enumerable.Range(0, 24).OrderByDescending(hour => hourlyTotals[hour]).ThenBy(hour => hour).First();
        var busiestDayIndex = dailyTotals.Count == 0 ? -1 : dailyTotals.IndexOf(dailyTotals.Max());
        var topAction = actionBreakdown.FirstOrDefault(item => item.Label != OtherLabel);

        var pageSize = filter.PageSize;
        var totalPages = Math.Max(1, (int)Math.Ceiling(logs.Count / (double)pageSize));
        var page = Math.Min(filter.Page, totalPages);
        var records = logs
            .OrderByDescending(log => log.CreatedTimeLog)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new CustomerBehaviorReportViewModel
        {
            Filter = filter,
            MaxRangeDays = MaxRangeDays,
            TotalCalls = logs.Count,
            SourceTotalCalls = source.Count,
            UniqueLoans = uniqueLoans,
            UniqueCustomers = logs
                .Select(log => NormalizeName(log.FullName))
                .Where(name => name.Length > 0)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            AverageCallsPerLoan = uniqueLoans == 0 ? 0 : Math.Round(logs.Count / (double)uniqueLoans, 1),
            DisbursedLoans = disbursedLoans,
            DisbursedLoanRate = uniqueLoans == 0 ? 0 : Math.Round(disbursedLoans * 100d / uniqueLoans, 1),
            ReturningLoans = returningLoans,
            PeakHourLabel = logs.Count == 0 ? null : $"{peakHour:00}:00 - {peakHour:00}:59",
            PeakHourCalls = hourlyTotals[peakHour],
            BusiestDayLabel = busiestDayIndex < 0 || dailyTotals[busiestDayIndex] == 0 ? null : days[busiestDayIndex].ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            BusiestDayCalls = busiestDayIndex < 0 ? 0 : dailyTotals[busiestDayIndex],
            TopActionName = topAction?.Label,
            TopActionShare = topAction is null || logs.Count == 0 ? 0 : Math.Round(topAction.Value * 100d / logs.Count, 1),
            ActionOptions = source
                .GroupBy(log => log.ActionCall)
                .Select(group => new CustomerBehaviorOption { Value = group.Key.ToString(CultureInfo.InvariantCulture), Label = ActionLabel(group.First()) })
                .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            ProductOptions = source
                .Where(log => log.ProductId.HasValue)
                .GroupBy(log => log.ProductId!.Value)
                .Select(group => new CustomerBehaviorOption { Value = group.Key.ToString(CultureInfo.InvariantCulture), Label = TextOrUnknown(group.First().ProductName) })
                .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            StatusOptions = source
                .Where(log => log.Status.HasValue)
                .GroupBy(log => log.Status!.Value)
                .Select(group => new CustomerBehaviorOption { Value = group.Key.ToString(CultureInfo.InvariantCulture), Label = TextOrUnknown(group.First().StatusName) })
                .OrderBy(option => option.Label, StringComparer.CurrentCultureIgnoreCase)
                .ToList(),
            PartnerOptions = source
                .Select(log => log.DisbursementPartner?.Trim())
                .Where(partner => !string.IsNullOrWhiteSpace(partner))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(partner => partner, StringComparer.CurrentCultureIgnoreCase)
                .Select(partner => new CustomerBehaviorOption { Value = partner!, Label = partner! })
                .ToList(),
            DailyLabels = days.Select(day => day.ToString("dd/MM", CultureInfo.InvariantCulture)).ToList(),
            DailyTotals = dailyTotals,
            DailyUniqueLoans = dailyUniqueLoans,
            DailyActionSeries = dailyActionSeries,
            HourlyTotals = hourlyTotals,
            Heatmap = Enumerable.Range(0, 7)
                .Select(dayIndex => new CustomerBehaviorHeatmapRow
                {
                    DayLabel = WeekdayLabels[dayIndex],
                    Cells = Enumerable.Range(0, 24).Select(hour => heatmap[dayIndex, hour]).ToList()
                })
                .ToList(),
            HeatmapMax = heatmap.Cast<long>().DefaultIfEmpty(0).Max(),
            ActionBreakdown = actionBreakdown,
            ProductBreakdown = BuildBreakdown(logs, log => TextOrUnknown(log.ProductName), BreakdownLimit),
            StatusBreakdown = BuildBreakdown(logs, log => TextOrUnknown(log.StatusName), BreakdownLimit),
            PartnerBreakdown = BuildBreakdown(logs, log => TextOrUnknown(log.DisbursementPartner), BreakdownLimit),
            TopCustomers = loans
                .Select(group =>
                {
                    var latest = group.MaxBy(log => log.CreatedTimeLog)!;
                    var topActionGroup = group.GroupBy(ActionLabel).OrderByDescending(item => item.Count()).First();
                    return new CustomerBehaviorTopCustomer
                    {
                        LoanBriefId = group.Key,
                        FullName = TextOrUnknown(latest.FullName),
                        ProductName = TextOrUnknown(latest.ProductName),
                        StatusName = TextOrUnknown(latest.StatusName),
                        LoanAmount = latest.LoanAmount,
                        Calls = group.Count(),
                        DistinctActions = group.Select(log => log.ActionCall).Distinct().Count(),
                        TopAction = topActionGroup.Key,
                        TopActionCalls = topActionGroup.Count(),
                        FirstCallAt = group.Min(log => log.CreatedTimeLog),
                        LastCallAt = latest.CreatedTimeLog,
                        ActiveDays = group.Select(log => log.CreatedTimeLog.Date).Distinct().Count()
                    };
                })
                .OrderByDescending(customer => customer.Calls)
                .ThenByDescending(customer => customer.LastCallAt)
                .Take(TopCustomerLimit)
                .ToList(),
            Records = new PagedResult<CustomerApiCallLog>
            {
                Items = records,
                TotalCount = logs.Count,
                Page = page,
                PageSize = pageSize
            }
        };
    }

    private static IEnumerable<CustomerApiCallLog> ApplyFilter(IEnumerable<CustomerApiCallLog> logs, CustomerBehaviorFilterViewModel filter)
    {
        if (filter.ActionCall.HasValue)
        {
            logs = logs.Where(log => log.ActionCall == filter.ActionCall.Value);
        }

        if (filter.ProductId.HasValue)
        {
            logs = logs.Where(log => log.ProductId == filter.ProductId.Value);
        }

        if (filter.Status.HasValue)
        {
            logs = logs.Where(log => log.Status == filter.Status.Value);
        }

        if (filter.Partner is not null)
        {
            logs = logs.Where(log => string.Equals(log.DisbursementPartner?.Trim(), filter.Partner, StringComparison.OrdinalIgnoreCase));
        }

        if (filter.SearchTerm is not null)
        {
            var term = filter.SearchTerm;
            logs = long.TryParse(term, NumberStyles.None, CultureInfo.InvariantCulture, out var loanBriefId)
                ? logs.Where(log => log.LoanBriefId == loanBriefId)
                : logs.Where(log => log.FullName?.Contains(term, StringComparison.CurrentCultureIgnoreCase) == true);
        }

        return logs;
    }

    private static IReadOnlyList<BreakdownItem> BuildBreakdown(IReadOnlyCollection<CustomerApiCallLog> logs, Func<CustomerApiCallLog, string> selector, int limit)
    {
        var groups = logs
            .GroupBy(selector)
            .Select(group => new BreakdownItem { Label = group.Key, Value = group.Count() })
            .OrderByDescending(item => item.Value)
            .ToList();

        if (groups.Count <= limit)
        {
            return groups;
        }

        var top = groups.Take(limit - 1).ToList();
        top.Add(new BreakdownItem { Label = OtherLabel, Value = groups.Skip(limit - 1).Sum(item => item.Value) });
        return top;
    }

    private static string ActionLabel(CustomerApiCallLog log)
    {
        return string.IsNullOrWhiteSpace(log.ActionCallName) ? $"Action #{log.ActionCall}" : log.ActionCallName.Trim();
    }

    private static string TextOrUnknown(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? UnknownLabel : value.Trim();
    }

    private static string NormalizeName(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? string.Empty : string.Join(' ', value.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    private static int ToMondayFirstIndex(DayOfWeek dayOfWeek)
    {
        return dayOfWeek == DayOfWeek.Sunday ? 6 : (int)dayOfWeek - 1;
    }
}
