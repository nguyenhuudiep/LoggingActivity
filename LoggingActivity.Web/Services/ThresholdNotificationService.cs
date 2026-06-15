using LoggingActivity.Web.Options;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;

namespace LoggingActivity.Web.Services;

public sealed class ThresholdNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<ThresholdNotificationOptions> _options;
    private readonly ILogger<ThresholdNotificationService> _logger;

    public ThresholdNotificationService(
        HttpClient httpClient,
        IOptions<ThresholdNotificationOptions> options,
        ILogger<ThresholdNotificationService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task NotifyAsync(string message, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        if (!options.Enabled || string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint) || options.UserIds.Count == 0)
        {
            _logger.LogWarning("ThresholdNotification is enabled but endpoint or user list is missing.");
            return;
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var timeoutSeconds = options.TimeoutSeconds <= 0 ? 10 : options.TimeoutSeconds;
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var payload = new GapoworkPayload
        {
            UserIds = options.UserIds,
            Message = BuildMessage(message)
        };

        try
        {
            using var response = await _httpClient.PostAsJsonAsync(options.Endpoint, payload, timeoutCts.Token);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Threshold notification API returned status {StatusCode}.", (int)response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to call threshold notification API.");
        }
    }

    private sealed class GapoworkPayload
    {
        public List<int> UserIds { get; set; } = new();

        public string Message { get; set; } = string.Empty;
    }

    private static string BuildMessage(string message)
    {
        const string prefix = "Logging - ";
        return message.StartsWith(prefix, StringComparison.Ordinal) ? message : $"{prefix}{message}";
    }
}