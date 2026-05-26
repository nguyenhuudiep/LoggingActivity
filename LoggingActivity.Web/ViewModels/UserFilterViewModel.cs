namespace LoggingActivity.Web.ViewModels;

public sealed class UserFilterViewModel
{
    public string? SearchTerm { get; set; }

    public string? Role { get; set; }

    public bool? IsActive { get; set; }

    public int Page { get; set; } = 1;

    public int PageSize { get; set; } = 10;
}