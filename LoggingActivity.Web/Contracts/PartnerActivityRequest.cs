using System.ComponentModel.DataAnnotations;

namespace LoggingActivity.Web.Contracts;

public sealed class PartnerActivityRequest
{
    [Range(1, int.MaxValue)]
    public int UserId { get; set; }

    [Required]
    public string UserName { get; set; } = string.Empty;

    [Required]
    public string Action { get; set; } = string.Empty;

    [Required]
    public string Description { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;
}