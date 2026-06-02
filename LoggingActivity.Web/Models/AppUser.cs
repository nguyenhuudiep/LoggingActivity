using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace LoggingActivity.Web.Models;

[BsonIgnoreExtraElements]
public sealed class AppUser
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string? Id { get; set; }

    public string UserName { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string Role { get; set; } = SystemRoles.Auditor;

    public List<string> PermissionGroupIds { get; set; } = new();

    public List<string> FunctionPermissions { get; set; } = new();

    public List<string> CustomFunctionPermissions { get; set; } = new();

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}