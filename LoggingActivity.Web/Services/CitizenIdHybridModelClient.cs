using System.Net.Http.Headers;
using System.Text.Json;
using LoggingActivity.Web.Contracts;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Options;
using Microsoft.Extensions.Options;

namespace LoggingActivity.Web.Services;

public sealed class CitizenIdHybridModelClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<CitizenIdHybridModelOptions> _options;
    private readonly ILogger<CitizenIdHybridModelClient> _logger;

    public CitizenIdHybridModelClient(
        HttpClient httpClient,
        IOptionsMonitor<CitizenIdHybridModelOptions> options,
        ILogger<CitizenIdHybridModelClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<CitizenIdHybridPrediction?> TryPredictAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled || string.IsNullOrWhiteSpace(options.Endpoint) || imageBytes.Length == 0)
        {
            return null;
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 1, 30)));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        using var request = new HttpRequestMessage(HttpMethod.Post, options.Endpoint.Trim());
        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            request.Headers.Remove(options.ApiKeyHeaderName);
            request.Headers.Add(options.ApiKeyHeaderName, options.ApiKey.Trim());
        }

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(imageContent, "image", "citizen-id.jpg");
        request.Content = content;

        try
        {
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Hybrid citizen-id model returned non-success status code {StatusCode}.", (int)response.StatusCode);
                return null;
            }

            try
            {
                await using var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
                using var document = await JsonDocument.ParseAsync(stream, cancellationToken: linkedCts.Token);
                var root = document.RootElement;

                if (!TryGetString(root, "side", out var sideRaw))
                {
                    return null;
                }

                var side = sideRaw.Trim().ToLowerInvariant();
                if (!string.Equals(side, CitizenIdDetectedSides.Front, StringComparison.Ordinal)
                    && !string.Equals(side, CitizenIdDetectedSides.Back, StringComparison.Ordinal))
                {
                    return null;
                }

                var confidence = TryGetDouble(root, "confidence", out var parsedConfidence)
                    ? Math.Clamp(parsedConfidence, 0, 1)
                    : 0;

                if (confidence < options.MinConfidence)
                {
                    return null;
                }

                var reasons = ExtractReasons(root);
                return new CitizenIdHybridPrediction
                {
                    Side = side,
                    Confidence = confidence,
                    Reasons = reasons
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Cannot parse hybrid citizen-id model response.");
                return null;
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Hybrid citizen-id model request timed out.");
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hybrid citizen-id model request failed.");
            return null;
        }
    }

    private static bool TryGetString(JsonElement element, string propertyName, out string value)
    {
        value = string.Empty;
        if (!TryGetProperty(element, propertyName, out var property) || property.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        value = property.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static bool TryGetDouble(JsonElement element, string propertyName, out double value)
    {
        value = 0;
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.Number)
        {
            return property.TryGetDouble(out value);
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return double.TryParse(property.GetString(), out value);
        }

        return false;
    }

    private static IReadOnlyList<string> ExtractReasons(JsonElement root)
    {
        if (!TryGetProperty(root, "reasons", out var reasonsElement) || reasonsElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var reasons = new List<string>();
        foreach (var item in reasonsElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var text = item.GetString();
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            reasons.Add(text.Trim());
            if (reasons.Count >= 3)
            {
                break;
            }
        }

        return reasons;
    }

    private static bool TryGetProperty(JsonElement element, string propertyName, out JsonElement property)
    {
        foreach (var current in element.EnumerateObject())
        {
            if (string.Equals(current.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                property = current.Value;
                return true;
            }
        }

        property = default;
        return false;
    }
}

public sealed class CitizenIdHybridPrediction
{
    public string Side { get; init; } = CitizenIdDetectedSides.Unknown;

    public double Confidence { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();
}