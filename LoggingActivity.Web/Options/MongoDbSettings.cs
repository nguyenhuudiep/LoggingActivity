namespace LoggingActivity.Web.Options;

public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string DatabaseName { get; set; } = "logging_activity_db";

    public string UsersCollectionName { get; set; } = "users";

    public string PartnersCollectionName { get; set; } = "partners";

    public string ActivityLogsCollectionName { get; set; } = "activity_logs";

    public string AlertRulesCollectionName { get; set; } = "alert_rules";

    public string LogActionDefinitionsCollectionName { get; set; } = "log_action_definitions";

    public string AlertHistoriesCollectionName { get; set; } = "alert_histories";
}