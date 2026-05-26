using System.ComponentModel.DataAnnotations;

namespace LoggingActivity.Web.ViewModels;

public sealed class LogActionDefinitionInputViewModel
{
    public string? ExistingCode { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã action.")]
    public string Code { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}