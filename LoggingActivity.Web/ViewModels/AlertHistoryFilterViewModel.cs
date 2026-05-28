namespace LoggingActivity.Web.ViewModels;

public sealed class AlertHistoryFilterViewModel
{
    public string? SearchTerm { get; set; }

    public string? PartnerId { get; set; }

    public string? Action { get; set; }

    public string? Status { get; set; }

    public DateTime? From { get; set; }

    public DateTime? To { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}