using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LoggingActivity.Web.Models;

[BsonIgnoreExtraElements]
public sealed class SystemAccessLog
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public string SessionId { get; set; } = string.Empty;

    public string EventType { get; set; } = SystemAccessEventTypes.Access;

    public string Description { get; set; } = string.Empty;

    public string Endpoint { get; set; } = string.Empty;

    public string HttpMethod { get; set; } = string.Empty;

    public string IpAddress { get; set; } = string.Empty;

    public string UserAgent { get; set; } = string.Empty;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public static class SystemAccessEventTypes
{
    public const string Login = "LOGIN";
    public const string Logout = "LOGOUT";
    public const string Access = "ACCESS";
    public const string SessionReplaced = "SESSION_REPLACED";
    public const string SessionRejected = "SESSION_REJECTED";

    public static readonly IReadOnlyList<string> All =
    [
        Login,
        Logout,
        Access,
        SessionReplaced,
        SessionRejected
    ];
}
