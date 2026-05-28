using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LoggingActivity.Web.Models;

public sealed class ActivityLogIngestQueueItem
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string DeduplicationKey { get; set; } = string.Empty;

    public string RequestId { get; set; } = string.Empty;

    [BsonRepresentation(BsonType.ObjectId)]
    public string? PartnerId { get; set; }

    public string PartnerName { get; set; } = "N/A";

    public int? ExternalUserId { get; set; }

    public string ActorIdentifier { get; set; } = string.Empty;

    public string ActorIdentifierType { get; set; } = ActorIdentifierTypes.Unknown;

    public string UserName { get; set; } = "Anonymous";

    public string Action { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string Source { get; set; } = ActivityLogSources.IntegratedApi;

    public string HttpMethod { get; set; } = "POST";

    public string? IpAddress { get; set; }

    public string Status { get; set; } = ActivityLogIngestQueueStatuses.Pending;

    public int AttemptCount { get; set; }

    public string LastError { get; set; } = string.Empty;

    public string? LeaseOwner { get; set; }

    public DateTime ReceivedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime AvailableAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? LeaseExpiresAtUtc { get; set; }

    public DateTime? ProcessedAtUtc { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    [BsonIgnore]
    public string DisplayActorIdentifier => ActorIdentityHelper.ResolveIdentifier(this);

    [BsonIgnore]
    public string DisplayActorIdentifierType => ActorIdentityHelper.ResolveType(this);
}

public static class ActivityLogIngestQueueStatuses
{
    public const string Pending = "Pending";
    public const string Processing = "Processing";
    public const string Completed = "Completed";
    public const string Failed = "Failed";
}