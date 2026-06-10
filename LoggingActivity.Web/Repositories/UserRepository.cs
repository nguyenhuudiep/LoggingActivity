using LoggingActivity.Web.Data;
using LoggingActivity.Web.Models;
using MongoDB.Driver;
using MongoDB.Bson;
using System.Text.RegularExpressions;

namespace LoggingActivity.Web.Repositories;

public sealed class UserRepository : IUserRepository
{
    private readonly MongoDbContext _context;

    public UserRepository(MongoDbContext context)
    {
        _context = context;
    }

    public Task<AppUser?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return _context.Users.Find(user => user.Id == id).FirstOrDefaultAsync(cancellationToken)!;
    }

    public Task<AppUser?> GetByUserNameAsync(string userName, CancellationToken cancellationToken = default)
    {
        var normalizedUserName = string.IsNullOrWhiteSpace(userName)
            ? string.Empty
            : userName.Trim();
        var exactRegex = new BsonRegularExpression($"^{Regex.Escape(normalizedUserName)}$", "i");

        return GetByUserNameCoreAsync(exactRegex, cancellationToken);
    }

    private async Task<AppUser?> GetByUserNameCoreAsync(BsonRegularExpression exactRegex, CancellationToken cancellationToken)
    {
        try
        {
            return await _context.Users
                .Find(Builders<AppUser>.Filter.Regex(user => user.UserName, exactRegex))
                .FirstOrDefaultAsync(cancellationToken);
        }
        catch
        {
            // Fallback for legacy documents that cannot be deserialized into AppUser.
            var rawUsers = _context.Users.Database.GetCollection<BsonDocument>(_context.Users.CollectionNamespace.CollectionName);
            var rawUser = await rawUsers
                .Find(Builders<BsonDocument>.Filter.Regex("UserName", exactRegex))
                .FirstOrDefaultAsync(cancellationToken);

            return rawUser is null ? null : MapAppUser(rawUser);
        }
    }

    public async Task<IReadOnlyList<AppUser>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Users.Find(FilterDefinition<AppUser>.Empty)
            .SortBy(user => user.UserName)
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResult<AppUser>> GetPagedAsync(UserQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);
        var page = query.Page < 1 ? 1 : query.Page;
        var pageSize = query.PageSize is < 1 or > 100 ? 10 : query.PageSize;

        var totalCount = await _context.Users.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var items = await _context.Users.Find(filter)
            .SortBy(user => user.UserName)
            .Skip((page - 1) * pageSize)
            .Limit(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AppUser>
        {
            Items = items,
            TotalCount = totalCount,
            Page = page,
            PageSize = pageSize
        };
    }

    public async Task<UserStatistics> GetStatisticsAsync(UserQuery query, CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(query);

        var totalUsers = await _context.Users.CountDocumentsAsync(filter, cancellationToken: cancellationToken);
        var activeUsers = await _context.Users.CountDocumentsAsync(
            filter & Builders<AppUser>.Filter.Eq(user => user.IsActive, true),
            cancellationToken: cancellationToken);
        var inactiveUsers = await _context.Users.CountDocumentsAsync(
            filter & Builders<AppUser>.Filter.Eq(user => user.IsActive, false),
            cancellationToken: cancellationToken);

        return new UserStatistics
        {
            TotalUsers = totalUsers,
            ActiveUsers = activeUsers,
            InactiveUsers = inactiveUsers
        };
    }

    public Task<long> CountByRoleAsync(string role, CancellationToken cancellationToken = default)
    {
        return _context.Users.CountDocumentsAsync(user => user.Role == role, cancellationToken: cancellationToken);
    }

    public Task<long> CountByPermissionGroupIdAsync(string permissionGroupId, CancellationToken cancellationToken = default)
    {
        return _context.Users.CountDocumentsAsync(
            Builders<AppUser>.Filter.AnyEq(user => user.PermissionGroupIds, permissionGroupId),
            cancellationToken: cancellationToken);
    }

    public async Task<IReadOnlyList<AppUser>> GetByPermissionGroupIdAsync(string permissionGroupId, CancellationToken cancellationToken = default)
    {
        return await _context.Users.Find(Builders<AppUser>.Filter.AnyEq(user => user.PermissionGroupIds, permissionGroupId))
            .ToListAsync(cancellationToken);
    }

    public Task CreateAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        return _context.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
    }

    public Task UpdateAsync(AppUser user, CancellationToken cancellationToken = default)
    {
        user.UpdatedAtUtc = DateTime.UtcNow;
        return _context.Users.ReplaceOneAsync(existing => existing.Id == user.Id, user, cancellationToken: cancellationToken);
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return _context.Users.DeleteOneAsync(user => user.Id == id, cancellationToken);
    }

    private static FilterDefinition<AppUser> BuildFilter(UserQuery query)
    {
        var builder = Builders<AppUser>.Filter;
        var filters = new List<FilterDefinition<AppUser>>();

        if (!string.IsNullOrWhiteSpace(query.SearchTerm))
        {
            var searchTerm = query.SearchTerm.Trim();
            var regex = new BsonRegularExpression(searchTerm, "i");

            filters.Add(builder.Or(
                builder.Regex(user => user.UserName, regex),
                builder.Regex(user => user.DisplayName, regex),
                builder.Regex(user => user.Email, regex)));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            filters.Add(builder.Eq(user => user.Role, query.Role));
        }

        if (query.IsActive.HasValue)
        {
            filters.Add(builder.Eq(user => user.IsActive, query.IsActive.Value));
        }

        return filters.Count == 0 ? builder.Empty : builder.And(filters);
    }

    private static AppUser MapAppUser(BsonDocument document)
    {
        return new AppUser
        {
            Id = ReadId(document),
            UserName = ReadString(document, "UserName"),
            DisplayName = ReadString(document, "DisplayName"),
            Email = ReadString(document, "Email"),
            PasswordHash = ReadString(document, "PasswordHash"),
            Role = ReadString(document, "Role", SystemRoles.Auditor),
            PermissionGroupIds = ReadStringList(document, "PermissionGroupIds"),
            FunctionPermissions = ReadStringList(document, "FunctionPermissions"),
            CustomFunctionPermissions = ReadStringList(document, "CustomFunctionPermissions"),
            IsActive = ReadBool(document, "IsActive", true),
            CreatedAtUtc = ReadDateTime(document, "CreatedAtUtc"),
            UpdatedAtUtc = ReadDateTime(document, "UpdatedAtUtc")
        };
    }

    private static string? ReadId(BsonDocument document)
    {
        if (!document.TryGetValue("_id", out var value) || value.IsBsonNull)
        {
            return null;
        }

        if (value.BsonType == BsonType.ObjectId)
        {
            return value.AsObjectId.ToString();
        }

        return value.ToString();
    }

    private static string ReadString(BsonDocument document, string name, string defaultValue = "")
    {
        if (!document.TryGetValue(name, out var value) || value.IsBsonNull)
        {
            return defaultValue;
        }

        if (value.BsonType == BsonType.String)
        {
            var raw = value.AsString;
            return string.IsNullOrWhiteSpace(raw) ? defaultValue : raw.Trim();
        }

        var text = value.ToString();
        return string.IsNullOrWhiteSpace(text) ? defaultValue : text.Trim();
    }

    private static List<string> ReadStringList(BsonDocument document, string name)
    {
        if (!document.TryGetValue(name, out var value) || value.IsBsonNull)
        {
            return new List<string>();
        }

        if (value.BsonType == BsonType.Array)
        {
            return value.AsBsonArray
                .Select(item => item?.ToString())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .Select(item => item!.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var singleValue = value.ToString();
        if (string.IsNullOrWhiteSpace(singleValue))
        {
            return new List<string>();
        }

        return new List<string> { singleValue.Trim() };
    }

    private static bool ReadBool(BsonDocument document, string name, bool defaultValue)
    {
        if (!document.TryGetValue(name, out var value) || value.IsBsonNull)
        {
            return defaultValue;
        }

        if (value.BsonType == BsonType.Boolean)
        {
            return value.AsBoolean;
        }

        if (value.BsonType is BsonType.Int32 or BsonType.Int64)
        {
            return value.ToInt64() != 0;
        }

        if (bool.TryParse(value.ToString(), out var parsed))
        {
            return parsed;
        }

        return defaultValue;
    }

    private static DateTime ReadDateTime(BsonDocument document, string name)
    {
        if (!document.TryGetValue(name, out var value) || value.IsBsonNull)
        {
            return DateTime.UtcNow;
        }

        if (value.BsonType == BsonType.DateTime)
        {
            return value.ToUniversalTime();
        }

        if (DateTime.TryParse(value.ToString(), out var parsed))
        {
            return parsed.Kind == DateTimeKind.Utc ? parsed : parsed.ToUniversalTime();
        }

        return DateTime.UtcNow;
    }
}