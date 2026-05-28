using MongoDB.Bson.Serialization.Attributes;

namespace LoggingActivity.Web.Models;

public static class ActorIdentifierTypes
{
    public const string UserId = "user-id";
    public const string Phone = "phone";
    public const string InternalUserId = "internal-user-id";
    public const string Unknown = "unknown";
}

public static class ActorIdentityHelper
{
    public static string NormalizeIdentifier(string? identifier)
    {
        return string.IsNullOrWhiteSpace(identifier) ? string.Empty : identifier.Trim();
    }

    public static string NormalizeType(string? identifierType, string? identifier)
    {
        var normalizedType = string.IsNullOrWhiteSpace(identifierType) ? string.Empty : identifierType.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(normalizedType))
        {
            return normalizedType switch
            {
                "userid" => ActorIdentifierTypes.UserId,
                "user_id" => ActorIdentifierTypes.UserId,
                "user-id" => ActorIdentifierTypes.UserId,
                "phone" => ActorIdentifierTypes.Phone,
                "sdt" => ActorIdentifierTypes.Phone,
                "phone-number" => ActorIdentifierTypes.Phone,
                "internal-user-id" => ActorIdentifierTypes.InternalUserId,
                _ => normalizedType
            };
        }

        var normalizedIdentifier = NormalizeIdentifier(identifier);
        if (LooksLikePhone(normalizedIdentifier))
        {
            return ActorIdentifierTypes.Phone;
        }

        return string.IsNullOrWhiteSpace(normalizedIdentifier)
            ? ActorIdentifierTypes.Unknown
            : ActorIdentifierTypes.UserId;
    }

    public static string ResolveIdentifier(ActivityLog log)
    {
        var actorIdentifier = NormalizeIdentifier(log.ActorIdentifier);
        if (!string.IsNullOrWhiteSpace(actorIdentifier))
        {
            return actorIdentifier;
        }

        if (log.ExternalUserId.HasValue)
        {
            return log.ExternalUserId.Value.ToString();
        }

        if (!IsIntegratedSource(log.Source))
        {
            return NormalizeIdentifier(log.UserId);
        }

        return string.Empty;
    }

    public static string ResolveType(ActivityLog log)
    {
        var actorType = NormalizeType(log.ActorIdentifierType, log.ActorIdentifier);
        if (!string.Equals(actorType, ActorIdentifierTypes.Unknown, StringComparison.OrdinalIgnoreCase))
        {
            return actorType;
        }

        if (log.ExternalUserId.HasValue)
        {
            return ActorIdentifierTypes.UserId;
        }

        if (!IsIntegratedSource(log.Source) && !string.IsNullOrWhiteSpace(log.UserId))
        {
            return ActorIdentifierTypes.InternalUserId;
        }

        return ActorIdentifierTypes.Unknown;
    }

    public static string ResolveIdentifier(ActivityLogIngestQueueItem item)
    {
        var actorIdentifier = NormalizeIdentifier(item.ActorIdentifier);
        if (!string.IsNullOrWhiteSpace(actorIdentifier))
        {
            return actorIdentifier;
        }

        return item.ExternalUserId?.ToString() ?? string.Empty;
    }

    public static string ResolveType(ActivityLogIngestQueueItem item)
    {
        var actorType = NormalizeType(item.ActorIdentifierType, item.ActorIdentifier);
        if (!string.Equals(actorType, ActorIdentifierTypes.Unknown, StringComparison.OrdinalIgnoreCase))
        {
            return actorType;
        }

        return item.ExternalUserId.HasValue ? ActorIdentifierTypes.UserId : ActorIdentifierTypes.Unknown;
    }

    public static string ResolveIdentifier(AlertHistory history)
    {
        var actorIdentifier = NormalizeIdentifier(history.ActorIdentifier);
        if (!string.IsNullOrWhiteSpace(actorIdentifier))
        {
            return actorIdentifier;
        }

        return history.UserId?.ToString() ?? string.Empty;
    }

    public static string ResolveType(AlertHistory history)
    {
        var actorType = NormalizeType(history.ActorIdentifierType, history.ActorIdentifier);
        if (!string.Equals(actorType, ActorIdentifierTypes.Unknown, StringComparison.OrdinalIgnoreCase))
        {
            return actorType;
        }

        return history.UserId.HasValue ? ActorIdentifierTypes.UserId : ActorIdentifierTypes.Unknown;
    }

    public static bool TryGetLegacyExternalUserId(string? identifier, out int userId)
    {
        return int.TryParse(NormalizeIdentifier(identifier), out userId);
    }

    public static string BuildDisplayLabel(string? identifierType)
    {
        return NormalizeType(identifierType, null) switch
        {
            ActorIdentifierTypes.Phone => "Số điện thoại",
            ActorIdentifierTypes.InternalUserId => "Mã tài khoản nội bộ",
            ActorIdentifierTypes.UserId => "User ID",
            _ => "Key"
        };
    }

    private static bool IsIntegratedSource(string? source)
    {
        return string.Equals(source, ActivityLogSources.IntegratedApi, StringComparison.OrdinalIgnoreCase)
            || string.Equals(source, ActivityLogSources.LegacyPartnerApi, StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikePhone(string identifier)
    {
        if (identifier.Length is < 10 or > 11 || !identifier.StartsWith('0'))
        {
            return false;
        }

        return identifier.All(char.IsDigit);
    }
}