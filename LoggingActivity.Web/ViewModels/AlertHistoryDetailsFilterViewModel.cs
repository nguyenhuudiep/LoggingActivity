namespace LoggingActivity.Web.ViewModels;

public sealed class AlertHistoryDetailsFilterViewModel
{
    public string? ActorIdentifier { get; set; }

    public string? ActorIdentifierType { get; set; }

    public string? PartnerId { get; set; }

    public string? Action { get; set; }

    public DateTime? AlertDate { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
