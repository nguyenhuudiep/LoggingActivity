using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LoggingActivity.Web.Models;

public sealed class ActivityLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? PartnerId { get; set; }

    public string? PartnerName { get; set; }

    [BsonRepresentation(BsonType.ObjectId)]
    public string? UserId { get; set; }

    public int? ExternalUserId { get; set; }

    public string UserName { get; set; } = "Anonymous";

    public string Role { get; set; } = string.Empty;

    public string Action { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string Source { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = string.Empty;

    public string? IpAddress { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class ActivityLogSources
{
    public const string IntegratedApi = "IntegratedApi";

    public const string LegacyPartnerApi = "PartnerApi";
}