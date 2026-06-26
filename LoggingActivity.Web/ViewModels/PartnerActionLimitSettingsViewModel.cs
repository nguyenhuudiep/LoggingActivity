using LoggingActivity.Web.Contracts;
using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class PartnerActionLimitSettingsViewModel
{
    public PartnerActionLimitFilterViewModel Filter { get; init; } = new();

    public IReadOnlyList<PartnerOptionViewModel> PartnerOptions { get; init; } = Array.Empty<PartnerOptionViewModel>();

    public IReadOnlyList<SelectOptionViewModel> UserKeyTypeOptions { get; init; } = Array.Empty<SelectOptionViewModel>();

    public IReadOnlyList<SelectOptionViewModel> ActionOptions { get; init; } = Array.Empty<SelectOptionViewModel>();

    public PagedResult<PartnerActionLimitListItemViewModel> Rules { get; init; } = new();

    public PartnerUserActionLimitUpsertRequest UpsertRequest { get; set; } = new();
}

public sealed class PartnerActionLimitListItemViewModel
{
    public string PartnerId { get; init; } = string.Empty;

    public string PartnerName { get; init; } = string.Empty;

    public string ActorIdentifier { get; init; } = string.Empty;

    public string ActorIdentifierType { get; init; } = string.Empty;

    public string Action { get; init; } = string.Empty;

    public int DailyLimit { get; init; }

    public bool IsActive { get; init; }
}

public sealed class PartnerOptionViewModel
{
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}

public sealed class SelectOptionViewModel
{
    public string Value { get; init; } = string.Empty;

    public string Label { get; init; } = string.Empty;
}
