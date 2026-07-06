namespace LoggingActivity.Web.Options;

public sealed class VehicleRegistrationOcrOptions
{
    public const string SectionName = "VehicleRegistrationOcr";

    public bool Enabled { get; set; }

    public string Provider { get; set; } = "openai";

    public string Endpoint { get; set; } = "https://api.openai.com/v1/responses";

    public string ApiKey { get; set; } = string.Empty;

    public string ApiKeyHeaderName { get; set; } = "Authorization";

    public string Model { get; set; } = "gpt-4.1";

    public string OpenAiEndpoint { get; set; } = string.Empty;

    public string OpenAiApiKey { get; set; } = string.Empty;

    public string OpenAiModel { get; set; } = string.Empty;

    public string CustomEndpoint { get; set; } = string.Empty;

    public string CustomApiKey { get; set; } = string.Empty;

    public string CustomApiKeyHeaderName { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 12;

    public double MinConfidence { get; set; } = 0.82;
}