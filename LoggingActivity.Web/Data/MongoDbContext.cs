using LoggingActivity.Web.Models;
using LoggingActivity.Web.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Text.Json;

namespace LoggingActivity.Web.Data;

public sealed class MongoDbContext
{
    private readonly IMongoDatabase _database;

    public MongoDbContext(IOptions<MongoDbSettings> options, ILogger<MongoDbContext> logger)
    {
        var settings = options.Value;
        var primaryConnectionString = BuildEffectiveConnectionString(settings.ConnectionString);
        var connectionString = primaryConnectionString;
        MongoUrl mongoUrl;
        MongoClient client;

        try
        {
            (mongoUrl, client) = CreateClient(connectionString);
            Ping(client, settings.DatabaseName, mongoUrl.DatabaseName);
        }
        catch (TimeoutException ex)
        {
            var fallbackConnectionString = BuildEffectiveConnectionString(TryReadDevelopmentConnectionString());
            if (string.IsNullOrWhiteSpace(fallbackConnectionString)
                || string.Equals(fallbackConnectionString, primaryConnectionString, StringComparison.Ordinal))
            {
                logger.LogError(ex, "MongoDB connection timed out and no distinct fallback connection string is available.");
                throw;
            }

            logger.LogWarning(ex, "MongoDB primary connection timed out. Falling back to appsettings.Development connection string.");
            connectionString = fallbackConnectionString;
            (mongoUrl, client) = CreateClient(connectionString);
            Ping(client, settings.DatabaseName, mongoUrl.DatabaseName);
        }

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
    }

    private static (MongoUrl Url, MongoClient Client) CreateClient(string connectionString)
    {
        var mongoUrl = MongoUrl.Create(connectionString);
        var clientSettings = MongoClientSettings.FromUrl(mongoUrl);
        clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(8);
        clientSettings.ConnectTimeout = TimeSpan.FromSeconds(8);
        clientSettings.SocketTimeout = TimeSpan.FromSeconds(30);
        var client = new MongoClient(clientSettings);
        return (mongoUrl, client);
    }

    private static void Ping(MongoClient client, string configuredDatabaseName, string? connectionStringDatabaseName)
    {
        var databaseName = !string.IsNullOrWhiteSpace(configuredDatabaseName)
            ? configuredDatabaseName
            : connectionStringDatabaseName;

        if (string.IsNullOrWhiteSpace(databaseName))
        {
            return;
        }

        client.GetDatabase(databaseName).RunCommand<BsonDocument>(new BsonDocument("ping", 1));
    }

    private static string TryReadDevelopmentConnectionString()
    {
        try
        {
            var filePath = Path.Combine(AppContext.BaseDirectory, "appsettings.Development.json");
            if (!File.Exists(filePath))
            {
                return string.Empty;
            }

            using var stream = File.OpenRead(filePath);
            using var document = JsonDocument.Parse(stream);
            if (!document.RootElement.TryGetProperty("MongoDb", out var mongoDbSection)
                || !mongoDbSection.TryGetProperty("ConnectionString", out var connectionString))
            {
                return string.Empty;
            }

            return connectionString.GetString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
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
}