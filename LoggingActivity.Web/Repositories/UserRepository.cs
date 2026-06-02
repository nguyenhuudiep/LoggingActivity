using LoggingActivity.Web.Data;
using LoggingActivity.Web.Models;
using MongoDB.Driver;
using MongoDB.Bson;

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
        return _context.Users.Find(user => user.UserName == userName).FirstOrDefaultAsync(cancellationToken)!;
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
}