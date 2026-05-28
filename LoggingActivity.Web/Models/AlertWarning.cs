namespace LoggingActivity.Web.Models;

public sealed class AlertWarning
{
    public string? PartnerId { get; init; }

    public string PartnerName { get; init; } = "N/A";

    public int? UserId { get; init; }

    public string ActorIdentifier { get; init; } = string.Empty;

    public string ActorIdentifierType { get; init; } = ActorIdentifierTypes.Unknown;

    public string UserName { get; init; } = "Anonymous";

    public string Action { get; init; } = string.Empty;

    public int DailyLimit { get; init; }

    public long CurrentCount { get; init; }

    public long ExceededBy => CurrentCount - DailyLimit;

    public bool IsAtLimit => CurrentCount == DailyLimit;

    public string DisplayActorIdentifier => string.IsNullOrWhiteSpace(ActorIdentifier) ? UserId?.ToString() ?? "N/A" : ActorIdentifier;

    public string DisplayActorIdentifierType => ActorIdentityHelper.NormalizeType(ActorIdentifierType, DisplayActorIdentifier);

    public string Message => $"Partner '{PartnerName}', key '{DisplayActorIdentifier}', user '{UserName}' da cham hoac vuot nguong {DailyLimit}/ngay cho action '{Action}' voi {CurrentCount} log.";
}