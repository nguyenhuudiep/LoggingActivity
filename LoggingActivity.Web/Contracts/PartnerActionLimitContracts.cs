using System.ComponentModel.DataAnnotations;

namespace LoggingActivity.Web.Contracts;

public sealed class PartnerActionLimitCheckRequest
{
    [Required]
    public string UserId { get; set; } = string.Empty;

    public string? UserKeyType { get; set; }

    [Required]
    public string Action { get; set; } = string.Empty;
}

public sealed class PartnerActionLimitCheckByKeyRequest
{
    [Required]
    public string PartnerApiKey { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public string? UserKeyType { get; set; }

    [Required]
    public string Action { get; set; } = string.Empty;
}

public sealed class PartnerUserActionLimitUpsertRequest
{
    [Required]
    public string PartnerId { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    public string? UserKeyType { get; set; }

    [Required]
    public string Action { get; set; } = string.Empty;

    [Range(1, 1000000)]
    public int DailyLimit { get; set; }

    public bool IsActive { get; set; } = true;
}

public sealed class PartnerUserActionLimitDeleteRequest
{
    [Required]
    public string PartnerId { get; set; } = string.Empty;

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public string Action { get; set; } = string.Empty;
}
