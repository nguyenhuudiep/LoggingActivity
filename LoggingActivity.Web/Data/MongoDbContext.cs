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
        var connectionString = BuildEffectiveConnectionString(settings.ConnectionString);
        var mongoUrl = MongoUrl.Create(connectionString);
        var clientSettings = MongoClientSettings.FromUrl(mongoUrl);
        clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(8);
        clientSettings.ConnectTimeout = TimeSpan.FromSeconds(8);
        clientSettings.SocketTimeout = TimeSpan.FromSeconds(30);
        var client = new MongoClient(clientSettings);

        var databaseName = !string.IsNullOrWhiteSpace(settings.DatabaseName)
            ? settings.DatabaseName
            : mongoUrl.DatabaseName;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            throw new InvalidOperationException("MongoDB database name must be provided either in MongoDb:DatabaseName or inside the connection string.");
        }

        _database = client.GetDatabase(databaseName);
        Users = _database.GetCollection<AppUser>(settings.UsersCollectionName);
        PermissionGroups = _database.GetCollection<PermissionGroup>(settings.PermissionGroupsCollectionName);
        Partners = _database.GetCollection<Partner>(settings.PartnersCollectionName);
        ActivityLogs = _database.GetCollection<ActivityLog>(settings.ActivityLogsCollectionName);
        ActivityLogIngestQueue = _database.GetCollection<ActivityLogIngestQueueItem>(settings.ActivityLogIngestQueueCollectionName);
        AlertRules = _database.GetCollection<AlertRule>(settings.AlertRulesCollectionName);
        LogActionDefinitions = _database.GetCollection<LogActionDefinition>(settings.LogActionDefinitionsCollectionName);
        AlertHistories = _database.GetCollection<AlertHistory>(settings.AlertHistoriesCollectionName);
        SystemAccessLogs = _database.GetCollection<SystemAccessLog>(settings.SystemAccessLogsCollectionName);
        UserActiveSessions = _database.GetCollection<UserActiveSession>(settings.UserActiveSessionsCollectionName);
    }

    private static string BuildEffectiveConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return connectionString;
        }

        var hasReplicaSet = connectionString.Contains("replicaSet=", StringComparison.OrdinalIgnoreCase);
        var hasDirectConnection = connectionString.Contains("directConnection=", StringComparison.OrdinalIgnoreCase);
        if (!hasReplicaSet || hasDirectConnection)
        {
            return connectionString;
        }

        return connectionString.Contains('?')
            ? $"{connectionString}&directConnection=true"
            : $"{connectionString}?directConnection=true";
    }

    public IMongoCollection<AppUser> Users { get; }

    public IMongoCollection<PermissionGroup> PermissionGroups { get; }

    public IMongoCollection<Partner> Partners { get; }

    public IMongoCollection<ActivityLog> ActivityLogs { get; }

    public IMongoCollection<ActivityLogIngestQueueItem> ActivityLogIngestQueue { get; }

    public IMongoCollection<AlertRule> AlertRules { get; }

    public IMongoCollection<LogActionDefinition> LogActionDefinitions { get; }

    public IMongoCollection<AlertHistory> AlertHistories { get; }

    public IMongoCollection<SystemAccessLog> SystemAccessLogs { get; }

    public IMongoCollection<UserActiveSession> UserActiveSessions { get; }
}