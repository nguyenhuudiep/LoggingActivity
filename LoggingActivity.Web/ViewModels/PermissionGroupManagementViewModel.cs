using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class PermissionGroupManagementViewModel
{
    public PermissionGroupFilterViewModel Filter { get; init; } = new();

    public PagedResult<PermissionGroup> Groups { get; init; } = new();

    public PermissionGroupStatistics Statistics { get; init; } = new();
}