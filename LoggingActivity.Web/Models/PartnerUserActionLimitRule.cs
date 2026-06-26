using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LoggingActivity.Web.Models;

public sealed class PartnerUserActionLimitRule
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string PartnerId { get; set; } = string.Empty;

    public string ActorIdentifier { get; set; } = string.Empty;

    public string ActorIdentifierType { get; set; } = ActorIdentifierTypes.Unknown;

    public string Action { get; set; } = string.Empty;

    public int DailyLimit { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class PartnerUserActionQuotaCheckResult
{
    public string PartnerId { get; init; } = string.Empty;

    public string ActorIdentifier { get; init; } = string.Empty;

    public string ActorIdentifierType { get; init; } = ActorIdentifierTypes.Unknown;

    public string Action { get; init; } = string.Empty;

    public bool HasLimit { get; init; }

    public int? DailyLimit { get; init; }

    public long UsedCount { get; init; }

    public long? RemainingCount { get; init; }

    public bool IsAllowed { get; init; }

    public string Message { get; init; } = string.Empty;
}
