namespace LoggingActivity.Web.Options;

public sealed class CitizenIdHybridModelOptions
{
    public const string SectionName = "CitizenIdHybridModel";

    public bool Enabled { get; set; }

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string ApiKeyHeaderName { get; set; } = "X-Api-Key";

    public int TimeoutSeconds { get; set; } = 4;

    public double MinConfidence { get; set; } = 0.72;
}