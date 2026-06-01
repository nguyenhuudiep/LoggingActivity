using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LoggingActivity.Web.Models;

[BsonIgnoreExtraElements]
public sealed class PermissionGroup
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public List<string> FunctionPermissions { get; set; } = new();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}