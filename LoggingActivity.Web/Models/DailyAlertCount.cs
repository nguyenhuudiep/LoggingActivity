namespace LoggingActivity.Web.Models;

public sealed class DailyAlertCount
{
    public DateTime AlertDateUtc { get; init; }

    public DateTime OccurredAtUtc { get; init; }

    public string? PartnerId { get; init; }

    public string PartnerName { get; init; } = "N/A";

    public int? UserId { get; init; }

    public string ActorIdentifier { get; init; } = string.Empty;

    public string ActorIdentifierType { get; init; } = ActorIdentifierTypes.Unknown;

    public string UserName { get; init; } = "Anonymous";

    public string Action { get; init; } = string.Empty;

    public long CurrentCount { get; init; }
}