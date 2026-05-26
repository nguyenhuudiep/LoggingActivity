namespace LoggingActivity.Web.Models;

public static class SystemRoles
{
    public const string Admin = "Admin";
    public const string Auditor = "Auditor";

    public static readonly IReadOnlyList<string> All = new[]
    {
        Admin,
        Auditor
    };
}