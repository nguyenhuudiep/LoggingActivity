namespace LoggingActivity.Web.ViewModels;

public sealed class PartnerActionLimitFilterViewModel
{
    public string? PartnerId { get; set; }

    public string? UserId { get; set; }

    public string? UserKeyType { get; set; }

    public string? Action { get; set; }

    public bool? IsActive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}
