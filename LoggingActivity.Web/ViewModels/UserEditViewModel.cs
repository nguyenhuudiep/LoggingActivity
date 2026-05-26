using System.ComponentModel.DataAnnotations;
using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.ViewModels;

public sealed class UserEditViewModel : IValidatableObject
{
    public string? Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập.")]
    [Display(Name = "Tên đăng nhập")]
    public string UserName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập tên hiển thị.")]
    [Display(Name = "Tên hiển thị")]
    public string DisplayName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng nhập email.")]
    [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
    public string Email { get; set; } = string.Empty;

    [Required(ErrorMessage = "Vui lòng chọn quyền.")]
    public string Role { get; set; } = SystemRoles.Auditor;

    [Display(Name = "Đang hoạt động")]
    public bool IsActive { get; set; } = true;

    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string? Password { get; set; }

    [DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu")]
    public string? ConfirmPassword { get; set; }

    public bool IsCreateMode { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (IsCreateMode && string.IsNullOrWhiteSpace(Password))
        {
            yield return new ValidationResult("Mật khẩu là bắt buộc khi tạo tài khoản.", new[] { nameof(Password) });
        }

        if (!string.Equals(Password, ConfirmPassword, StringComparison.Ordinal))
        {
            yield return new ValidationResult("Mật khẩu xác nhận không khớp.", new[] { nameof(ConfirmPassword) });
        }
    }
}