namespace LoggingActivity.Web.Options;

public sealed class ThresholdNotificationOptions
{
    public const string SectionName = "ThresholdNotification";

    public bool Enabled { get; set; } = true;

    public string Endpoint { get; set; } = "https://gatewaylos.tima.vn/api/gapowork/send_message";

    public List<int> UserIds { get; set; } = new() { 26880 };

    public int TimeoutSeconds { get; set; } = 10;
}