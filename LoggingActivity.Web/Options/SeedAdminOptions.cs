namespace LoggingActivity.Web.Options;

public sealed class SeedAdminOptions
{
    public const string SectionName = "SeedAdmin";

    public string UserName { get; set; } = "admin";

    public string DisplayName { get; set; } = "System Administrator";

    public string Email { get; set; } = "admin@example.com";

    public string Password { get; set; } = "Admin@123456";
}