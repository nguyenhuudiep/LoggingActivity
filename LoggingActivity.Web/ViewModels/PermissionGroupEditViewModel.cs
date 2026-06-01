using System.ComponentModel.DataAnnotations;

namespace LoggingActivity.Web.ViewModels;

public sealed class PermissionGroupEditViewModel : IValidatableObject
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên nhóm quyền.")]
    [Display(Name = "Tên nhóm quyền")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập mô tả.")]
    [Display(Name = "Mô tả")]
    public string Description { get; set; } = string.Empty;

    public List<string> SelectedPermissions { get; set; } = new();

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (SelectedPermissions is null || SelectedPermissions.Count == 0)
        {
            yield return new ValidationResult("Vui lòng chọn ít nhất một quyền chức năng cho nhóm quyền.", new[] { nameof(SelectedPermissions) });
        }
    }
}