using LoggingActivity.Web.Models;
using LoggingActivity.Web.Options;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace LoggingActivity.Web.Data;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoDbSettings> options)
    {
        var settings = options.Value;
        var client = new MongoClient(settings.ConnectionString);
        _database = client.GetDatabase(settings.DatabaseName);
        Users = _database.GetCollection<AppUser>(settings.UsersCollectionName);
        Partners = _database.GetCollection<Partner>(settings.PartnersCollectionName);
        ActivityLogs = _database.GetCollection<ActivityLog>(settings.ActivityLogsCollectionName);
        AlertRules = _database.GetCollection<AlertRule>(settings.AlertRulesCollectionName);
        LogActionDefinitions = _database.GetCollection<LogActionDefinition>(settings.LogActionDefinitionsCollectionName);
        AlertHistories = _database.GetCollection<AlertHistory>(settings.AlertHistoriesCollectionName);
    }

    public IMongoCollection<AppUser> Users { get; }

    public IMongoCollection<Partner> Partners { get; }

    public IMongoCollection<ActivityLog> ActivityLogs { get; }

    public IMongoCollection<AlertRule> AlertRules { get; }

    public IMongoCollection<LogActionDefinition> LogActionDefinitions { get; }

    public IMongoCollection<AlertHistory> AlertHistories { get; }
}