namespace LoggingActivity.Web.Options;

public sealed class CitizenIdOpenAiOptions
{
    public const string SectionName = "CitizenIdOpenAi";

    public bool Enabled { get; set; } = true;

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string DetectModel { get; set; } = string.Empty;

    public string OcrModel { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 12;

    public double MinConfidence { get; set; } = 0.78;
}