namespace LoggingActivity.Web.Options;

public sealed class MongoDbSettings
{
    public const string SectionName = "MongoDb";

    public string ConnectionString { get; set; } = "<set-via-user-secrets-or-env>";

    public string DatabaseName { get; set; } = string.Empty;

    public string UsersCollectionName { get; set; } = "users";

    public string PartnersCollectionName { get; set; } = "partners";

    public string ActivityLogsCollectionName { get; set; } = "activity_logs";

    public string PermissionGroupsCollectionName { get; set; } = "permission_groups";

    public string ActivityLogIngestQueueCollectionName { get; set; } = "activity_log_ingest_queue";

    public string AlertRulesCollectionName { get; set; } = "alert_rules";

    public string LogActionDefinitionsCollectionName { get; set; } = "log_action_definitions";

    public string AlertHistoriesCollectionName { get; set; } = "alert_histories";

    public string SystemAccessLogsCollectionName { get; set; } = "system_access_logs";

    public string UserActiveSessionsCollectionName { get; set; } = "user_active_sessions";

    public string PartnerUserActionLimitRulesCollectionName { get; set; } = "partner_user_action_limit_rules";
}