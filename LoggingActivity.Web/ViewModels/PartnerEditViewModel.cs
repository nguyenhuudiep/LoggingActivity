using System.ComponentModel.DataAnnotations;

namespace LoggingActivity.Web.ViewModels;

public sealed class PartnerEditViewModel
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên đối tác.")]
    [Display(Name = "Tên đối tác")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    public string? ApiKey { get; set; }

    public bool IsCreateMode { get; set; }
}