using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LoggingActivity.Web.Models;

public sealed class AlertHistory
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? PartnerId { get; set; }

    public string PartnerName { get; set; } = "N/A";

    public int? UserId { get; set; }

    public string ActorIdentifier { get; set; } = string.Empty;

    public string ActorIdentifierType { get; set; } = ActorIdentifierTypes.Unknown;

    public string UserName { get; set; } = "Anonymous";

    public string Action { get; set; } = string.Empty;

    public int DailyLimit { get; set; }

    public long CurrentCount { get; set; }

    public DateTime AlertDateUtc { get; set; }

    public DateTime OccurredAtUtc { get; set; }

    public string Message { get; set; } = string.Empty;

    [BsonIgnore]
    public string DisplayActorIdentifier => ActorIdentityHelper.ResolveIdentifier(this);

    [BsonIgnore]
    public string DisplayActorIdentifierType => ActorIdentityHelper.ResolveType(this);
}