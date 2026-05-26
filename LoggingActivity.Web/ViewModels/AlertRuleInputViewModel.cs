using System.ComponentModel.DataAnnotations;

namespace LoggingActivity.Web.ViewModels;

public sealed class AlertRuleInputViewModel
{
    public string? ExistingAction { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập action cần theo dõi.")]
    public string Action { get; set; } = string.Empty;

    [Range(1, 100000, ErrorMessage = "Ngưỡng theo ngày phải lớn hơn 0.")]
    public int DailyLimit { get; set; } = 10;

    public bool IsActive { get; set; } = true;
}