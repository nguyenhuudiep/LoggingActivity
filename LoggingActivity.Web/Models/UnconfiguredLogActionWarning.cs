namespace LoggingActivity.Web.Models;

public sealed class UnconfiguredLogActionWarning
{
    public string Action { get; init; } = string.Empty;

    public long Count { get; init; }

    public string Message => $"Action '{Action}' da xuat hien {Count} log nhung chua duoc cau hinh.";
}