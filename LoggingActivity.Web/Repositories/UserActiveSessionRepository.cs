using LoggingActivity.Web.Data;
using LoggingActivity.Web.Models;
using MongoDB.Driver;

namespace LoggingActivity.Web.Repositories;

public sealed class UserActiveSessionRepository : IUserActiveSessionRepository
{
    private readonly MongoDbContext _context;

    public UserActiveSessionRepository(MongoDbContext context)
    {
        _context = context;
    }

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<UserActiveSession>(
                Builders<UserActiveSession>.IndexKeys.Ascending(session => session.NormalizedUserName),
                new CreateIndexOptions { Name = "ux_user_active_sessions_username", Unique = true }),
            new CreateIndexModel<UserActiveSession>(
                Builders<UserActiveSession>.IndexKeys.Descending(session => session.LastSeenAtUtc),
                new CreateIndexOptions { Name = "ix_user_active_sessions_last_seen_desc" })
        };

        return _context.UserActiveSessions.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public Task<UserActiveSession?> GetByNormalizedUserNameAsync(string normalizedUserName, CancellationToken cancellationToken = default)
    {
        return _context.UserActiveSessions
            .Find(session => session.NormalizedUserName == normalizedUserName)
            .FirstOrDefaultAsync(cancellationToken)!;
    }

    public Task UpsertAsync(UserActiveSession session, CancellationToken cancellationToken = default)
    {
        var update = Builders<UserActiveSession>.Update
            .Set(existing => existing.UserName, session.UserName)
            .Set(existing => existing.UserId, session.UserId)
            .Set(existing => existing.DisplayName, session.DisplayName)
            .Set(existing => existing.Role, session.Role)
            .Set(existing => existing.SessionId, session.SessionId)
            .Set(existing => existing.IpAddress, session.IpAddress)
            .Set(existing => existing.UserAgent, session.UserAgent)
            .Set(existing => existing.LastSeenAtUtc, session.LastSeenAtUtc)
            .SetOnInsert(existing => existing.NormalizedUserName, session.NormalizedUserName)
            .SetOnInsert(existing => existing.IssuedAtUtc, session.IssuedAtUtc);

        return _context.UserActiveSessions.UpdateOneAsync(
            existing => existing.NormalizedUserName == session.NormalizedUserName,
            update,
            new UpdateOptions { IsUpsert = true },
            cancellationToken);
    }

    public async Task<bool> IsSessionActiveAsync(string normalizedUserName, string sessionId, CancellationToken cancellationToken = default)
    {
        var count = await _context.UserActiveSessions.CountDocumentsAsync(
            session => session.NormalizedUserName == normalizedUserName && session.SessionId == sessionId,
            cancellationToken: cancellationToken);
        return count > 0;
    }

    public Task TouchAsync(string normalizedUserName, string sessionId, DateTime seenAtUtc, CancellationToken cancellationToken = default)
    {
        var update = Builders<UserActiveSession>.Update.Set(session => session.LastSeenAtUtc, seenAtUtc);
        return _context.UserActiveSessions.UpdateOneAsync(
            session => session.NormalizedUserName == normalizedUserName && session.SessionId == sessionId,
            update,
            cancellationToken: cancellationToken);
    }

    public Task RemoveIfMatchesAsync(string normalizedUserName, string sessionId, CancellationToken cancellationToken = default)
    {
        return _context.UserActiveSessions.DeleteOneAsync(
            session => session.NormalizedUserName == normalizedUserName && session.SessionId == sessionId,
            cancellationToken);
    }
}
