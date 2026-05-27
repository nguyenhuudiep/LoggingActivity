using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class PartnerListViewModel
{
    public PartnerFilterViewModel Filter { get; init; } = new();

    public PagedResult<Partner> Partners { get; init; } = new();
}