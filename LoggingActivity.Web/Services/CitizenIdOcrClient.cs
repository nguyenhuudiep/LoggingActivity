using System.Net.Http.Headers;
using System.Text.Json;
using LoggingActivity.Web.Options;
using Microsoft.Extensions.Options;

namespace LoggingActivity.Web.Services;

public sealed class CitizenIdOcrClient
{
    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<CitizenIdOcrOptions> _options;
    private readonly ILogger<CitizenIdOcrClient> _logger;

    public CitizenIdOcrClient(
        HttpClient httpClient,
        IOptionsMonitor<CitizenIdOcrOptions> options,
        ILogger<CitizenIdOcrClient> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<CitizenIdOcrExecutionResult> TryExtractAsync(byte[] imageBytes, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return CitizenIdOcrExecutionResult.Disabled("OCR đang tắt trong cấu hình CitizenIdOcr:Enabled.");
        }

        if (string.IsNullOrWhiteSpace(options.Endpoint))
        {
            return CitizenIdOcrExecutionResult.NotConfigured("Thiếu CitizenIdOcr:Endpoint hoặc biến môi trường CITIZEN_ID_OCR_ENDPOINT.");
        }

        if (imageBytes.Length == 0)
        {
            return CitizenIdOcrExecutionResult.Failed("Ảnh đầu vào rỗng.");
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
                _logger.LogWarning("Citizen-id OCR endpoint returned non-success status code {StatusCode}.", (int)response.StatusCode);
                return CitizenIdOcrExecutionResult.Failed($"OCR endpoint trả về mã HTTP {(int)response.StatusCode}.");
            }

            await using var stream = await response.Content.ReadAsStreamAsync(linkedCts.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: linkedCts.Token);
            var root = document.RootElement;

            var rawText = TryGetString(root, "rawText", out var raw)
                ? raw
                : TryGetString(root, "text", out var text) ? text : string.Empty;

            var confidence = TryGetDouble(root, "confidence", out var parsedConfidence)
                ? Math.Clamp(parsedConfidence, 0, 1)
                : 0;

            var fields = ExtractStringDictionary(root, "fields");
            var lines = ExtractStringArray(root, "lines");

            if (string.IsNullOrWhiteSpace(rawText) && fields.Count == 0 && lines.Count == 0)
            {
                return CitizenIdOcrExecutionResult.NoData("OCR endpoint phản hồi thành công nhưng không có text/fields/lines.");
            }

            return CitizenIdOcrExecutionResult.Success(new CitizenIdOcrPrediction
            {
                RawText = rawText,
                Confidence = confidence,
                Fields = fields,
                Lines = lines
            });
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Citizen-id OCR request timed out.");
            return CitizenIdOcrExecutionResult.Timeout("OCR endpoint timeout.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Citizen-id OCR request failed.");
            return CitizenIdOcrExecutionResult.Failed("OCR request failed.");
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

    private static Dictionary<string, string> ExtractStringDictionary(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out var fieldsElement) || fieldsElement.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in fieldsElement.EnumerateObject())
        {
            if (item.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = item.Value.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result[item.Name] = value.Trim();
        }

        return result;
    }

    private static IReadOnlyList<string> ExtractStringArray(JsonElement root, string propertyName)
    {
        if (!TryGetProperty(root, propertyName, out var linesElement) || linesElement.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var result = new List<string>();
        foreach (var item in linesElement.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = item.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            result.Add(value.Trim());
        }

        return result;
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

public sealed class CitizenIdOcrPrediction
{
    public string RawText { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
}

public sealed class CitizenIdOcrExecutionResult
{
    public string Status { get; init; } = "disabled";

    public string Message { get; init; } = string.Empty;

    public CitizenIdOcrPrediction? Prediction { get; init; }

    public static CitizenIdOcrExecutionResult Success(CitizenIdOcrPrediction prediction)
    {
        return new CitizenIdOcrExecutionResult
        {
            Status = "success",
            Message = "OCR thành công.",
            Prediction = prediction
        };
    }

    public static CitizenIdOcrExecutionResult Disabled(string message)
    {
        return new CitizenIdOcrExecutionResult
        {
            Status = "disabled",
            Message = message
        };
    }

    public static CitizenIdOcrExecutionResult NotConfigured(string message)
    {
        return new CitizenIdOcrExecutionResult
        {
            Status = "not_configured",
            Message = message
        };
    }

    public static CitizenIdOcrExecutionResult Timeout(string message)
    {
        return new CitizenIdOcrExecutionResult
        {
            Status = "timeout",
            Message = message
        };
    }

    public static CitizenIdOcrExecutionResult NoData(string message)
    {
        return new CitizenIdOcrExecutionResult
        {
            Status = "no_data",
            Message = message
        };
    }

    public static CitizenIdOcrExecutionResult Failed(string message)
    {
        return new CitizenIdOcrExecutionResult
        {
            Status = "failed",
            Message = message
        };
    }
}