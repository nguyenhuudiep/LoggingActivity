namespace LoggingActivity.Web.ViewModels;

public sealed class DangerConfirmModalViewModel
{
    public string ModalId { get; init; } = string.Empty;

    public string LabelId { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string? Hint { get; init; }

    public string ConfirmText { get; init; } = "Xác nhận";

    public string FormId { get; init; } = string.Empty;
}