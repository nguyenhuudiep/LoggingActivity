using LoggingActivity.Web.Data;
using LoggingActivity.Web.Models;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.RegularExpressions;

namespace LoggingActivity.Web.Repositories;

public sealed class PartnerUserActionLimitRuleRepository : IPartnerUserActionLimitRuleRepository
{
    private readonly MongoDbContext _context;

    public PartnerUserActionLimitRuleRepository(MongoDbContext context)
    {
        _context = context;
    }

    public Task EnsureIndexesAsync(CancellationToken cancellationToken = default)
    {
        var indexes = new[]
        {
            new CreateIndexModel<PartnerUserActionLimitRule>(
                Builders<PartnerUserActionLimitRule>.IndexKeys
                    .Ascending(item => item.PartnerId)
                    .Ascending(item => item.ActorIdentifier)
                    .Ascending(item => item.Action),
                new CreateIndexOptions
                {
                    Name = "ux_partner_user_action_limit_scope",
                    Unique = true
                }),
            new CreateIndexModel<PartnerUserActionLimitRule>(
                Builders<PartnerUserActionLimitRule>.IndexKeys
                    .Ascending(item => item.PartnerId)
                    .Ascending(item => item.IsActive),
                new CreateIndexOptions { Name = "ix_partner_user_action_limit_partner_active" })
        };

        return _context.PartnerUserActionLimitRules.Indexes.CreateManyAsync(indexes, cancellationToken);
    }

    public async Task<IReadOnlyList<PartnerUserActionLimitRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.PartnerUserActionLimitRules.Find(Builders<PartnerUserActionLimitRule>.Filter.Empty)
            .SortBy(item => item.PartnerId)
            .ThenBy(item => item.ActorIdentifier)
            .ThenBy(item => item.Action)
            .ToListAsync(cancellationToken);
    }

    public Task<PartnerUserActionLimitRule?> GetByScopeAsync(string partnerId, string actorIdentifier, string action, CancellationToken cancellationToken = default)
    {
        var normalizedPartnerId = partnerId.Trim();
        var normalizedActorIdentifier = actorIdentifier.Trim();
        var normalizedAction = action.Trim();

        var filter = Builders<PartnerUserActionLimitRule>.Filter.Eq(item => item.PartnerId, normalizedPartnerId)
            & Builders<PartnerUserActionLimitRule>.Filter.Regex(item => item.ActorIdentifier, new BsonRegularExpression($"^{Regex.Escape(normalizedActorIdentifier)}$", "i"))
            & Builders<PartnerUserActionLimitRule>.Filter.Regex(item => item.Action, new BsonRegularExpression($"^{Regex.Escape(normalizedAction)}$", "i"));

        return _context.PartnerUserActionLimitRules.Find(filter).FirstOrDefaultAsync(cancellationToken)!;
    }

    public async Task<IReadOnlyList<PartnerUserActionLimitRule>> GetByPartnerAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        var normalizedPartnerId = partnerId.Trim();
        return await _context.PartnerUserActionLimitRules.Find(item => item.PartnerId == normalizedPartnerId)
            .SortBy(item => item.ActorIdentifier)
            .ThenBy(item => item.Action)
            .ToListAsync(cancellationToken);
    }

    public Task UpsertAsync(PartnerUserActionLimitRule rule, CancellationToken cancellationToken = default)
    {
        rule.UpdatedAtUtc = DateTime.UtcNow;

        if (string.IsNullOrWhiteSpace(rule.Id))
        {
            rule.Id = ObjectId.GenerateNewId().ToString();
            rule.CreatedAtUtc = DateTime.UtcNow;
        }

        var filter = Builders<PartnerUserActionLimitRule>.Filter.Eq(item => item.PartnerId, rule.PartnerId)
            & Builders<PartnerUserActionLimitRule>.Filter.Regex(item => item.ActorIdentifier, new BsonRegularExpression($"^{Regex.Escape(rule.ActorIdentifier)}$", "i"))
            & Builders<PartnerUserActionLimitRule>.Filter.Regex(item => item.Action, new BsonRegularExpression($"^{Regex.Escape(rule.Action)}$", "i"));

        return _context.PartnerUserActionLimitRules.ReplaceOneAsync(
            filter,
            rule,
            new ReplaceOptions { IsUpsert = true },
            cancellationToken);
    }

    public Task DeleteAsync(string partnerId, string actorIdentifier, string action, CancellationToken cancellationToken = default)
    {
        var normalizedPartnerId = partnerId.Trim();
        var normalizedActorIdentifier = actorIdentifier.Trim();
        var normalizedAction = action.Trim();

        var filter = Builders<PartnerUserActionLimitRule>.Filter.Eq(item => item.PartnerId, normalizedPartnerId)
            & Builders<PartnerUserActionLimitRule>.Filter.Regex(item => item.ActorIdentifier, new BsonRegularExpression($"^{Regex.Escape(normalizedActorIdentifier)}$", "i"))
            & Builders<PartnerUserActionLimitRule>.Filter.Regex(item => item.Action, new BsonRegularExpression($"^{Regex.Escape(normalizedAction)}$", "i"));

        return _context.PartnerUserActionLimitRules.DeleteOneAsync(filter, cancellationToken);
    }
}
