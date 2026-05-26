using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class UserManagementViewModel
{
    public UserFilterViewModel Filter { get; init; } = new();

    public PagedResult<AppUser> Users { get; init; } = new();

    public UserStatistics Statistics { get; init; } = new();
}