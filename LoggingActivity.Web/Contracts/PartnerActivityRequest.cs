using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using LoggingActivity.Web.Infrastructure;

namespace LoggingActivity.Web.Contracts;

public sealed class PartnerActivityRequest
{
    public string? RequestId { get; set; }

    [Required]
    [JsonConverter(typeof(FlexibleStringJsonConverter))]
    public string UserId { get; set; } = string.Empty;

    public string? UserKeyType { get; set; }

    public string? UserName { get; set; }

    [Required]
    public string Action { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;
}