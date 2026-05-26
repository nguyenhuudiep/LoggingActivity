namespace LoggingActivity.Web.Models;

public sealed class AlertWarning
{
    public string? PartnerId { get; init; }

    public string PartnerName { get; init; } = "N/A";

    public int? UserId { get; init; }

    public string UserName { get; init; } = "Anonymous";

    public string Action { get; init; } = string.Empty;

    public int DailyLimit { get; init; }

    public long CurrentCount { get; init; }

    public long ExceededBy => CurrentCount - DailyLimit;

    public bool IsAtLimit => CurrentCount == DailyLimit;

    public string Message => $"Partner '{PartnerName}', userId '{UserId}', user '{UserName}' da cham hoac vuot nguong {DailyLimit}/ngay cho action '{Action}' voi {CurrentCount} log.";
}