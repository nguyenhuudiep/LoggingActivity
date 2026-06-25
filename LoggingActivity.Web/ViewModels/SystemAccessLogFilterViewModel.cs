namespace LoggingActivity.Web.ViewModels;

public sealed class SystemAccessLogFilterViewModel
{
    public string? SearchTerm { get; set; }

    public string? EventType { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
