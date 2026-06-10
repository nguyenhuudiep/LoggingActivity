namespace LoggingActivity.Web.Options;

public sealed class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";

    public bool Enabled { get; set; } = true;

    public string UserName { get; set; } = "<set-via-user-secrets-or-env>";

    public string DisplayName { get; set; } = "System Administrator";

    public string Email { get; set; } = "<set-via-user-secrets-or-env>";

    public string Password { get; set; } = "<set-via-user-secrets-or-env>";
}