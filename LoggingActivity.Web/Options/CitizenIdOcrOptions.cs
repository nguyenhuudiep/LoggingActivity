namespace LoggingActivity.Web.Options;

public sealed class CitizenIdOcrOptions
{
    public const string SectionName = "CitizenIdOcr";

    public bool Enabled { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";

    public int TimeoutSeconds { get; set; } = 6;
}