using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LoggingActivity.Web.Contracts;
using LoggingActivity.Web.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LoggingActivity.Web.Services;

public sealed class CitizenIdOpenAiService
{
    private static readonly string[] RequiredOcrKeys =
    {
        "id_number",
        "full_name",
        "date_of_birth"
    };

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<CitizenIdOpenAiOptions> _options;
    private readonly IMemoryCache _cache;
    private readonly ILogger<CitizenIdOpenAiService> _logger;

    public CitizenIdOpenAiService(
        HttpClient httpClient,
        IOptionsMonitor<CitizenIdOpenAiOptions> options,
        IMemoryCache cache,
        ILogger<CitizenIdOpenAiService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _cache = cache;
        _logger = logger;
    }

    public async Task<CitizenIdSideDetectResponse> DetectSideAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return new CitizenIdSideDetectResponse
            {
                Side = CitizenIdDetectedSides.Unknown,
                Confidence = 0,
                Reasons = new[] { "Nhận diện CCCD đang tắt trong cấu hình CitizenIdOpenAi:Enabled." }
            };
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new CitizenIdSideDetectResponse
            {
                Side = CitizenIdDetectedSides.Unknown,
                Confidence = 0,
                Reasons = new[] { "Thiếu API key OpenAI cho nhận diện CCCD." }
            };
        }

        var endpoint = string.IsNullOrWhiteSpace(options.Endpoint)
            ? "https://api.openai.com/v1/responses"
            : options.Endpoint.Trim();

        var imageBytes = await NormalizeImageAsync(imageStream, cancellationToken);
        if (imageBytes.Length == 0)
        {
            return new CitizenIdSideDetectResponse
            {
                Side = CitizenIdDetectedSides.Unknown,
                Confidence = 0,
                Reasons = new[] { "Ảnh đầu vào rỗng." }
            };
        }

        var detectModel = ResolveDetectModel(options);
        var detectCacheKey = BuildCacheKey("detect", imageBytes, detectModel);
        if (_cache.TryGetValue<CitizenIdSideDetectResponse>(detectCacheKey, out var cachedDetect) && cachedDetect is not null)
        {
            return cachedDetect;
        }

        var prompt = "Phan loai anh CCCD Viet Nam thanh front/back/unknown. "
            + "Chi tra ve JSON hop le theo schema: "
            + "{\"side\":\"front|back|unknown\",\"confidence\":number(0..1),\"reasons\":string[]}. "
            + "Khong them markdown, khong giai thich ngoai JSON.";

        var payload = await ExecuteOpenAiAsync(endpoint, options, imageBytes, prompt, detectModel, maxOutputTokens: 220, cancellationToken);
        if (!payload.IsSuccess)
        {
            return new CitizenIdSideDetectResponse
            {
                Side = CitizenIdDetectedSides.Unknown,
                Confidence = 0,
                Reasons = new[] { payload.Message }
            };
        }

        if (!TryParseDetectSide(payload.Text, out var side, out var confidence, out var reasons))
        {
            return new CitizenIdSideDetectResponse
            {
                Side = CitizenIdDetectedSides.Unknown,
                Confidence = 0,
                Reasons = new[] { "OpenAI phản hồi thành công nhưng không parse được kết quả detect-side." }
            };
        }

        var detectResult = new CitizenIdSideDetectResponse
        {
            Side = side,
            Confidence = confidence,
            Reasons = reasons
        };

        _cache.Set(detectCacheKey, detectResult, TimeSpan.FromMinutes(5));
        return detectResult;
    }

    public async Task<CitizenIdOcrResult> ExtractOcrAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return new CitizenIdOcrResult
            {
                Applied = false,
                Status = "disabled",
                Message = "OCR CCCD đang tắt trong cấu hình CitizenIdOpenAi:Enabled.",
                RawText = string.Empty,
                Confidence = 0,
                Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Lines = Array.Empty<string>()
            };
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new CitizenIdOcrResult
            {
                Applied = false,
                Status = "not_configured",
                Message = "Thiếu API key OpenAI cho OCR CCCD.",
                RawText = string.Empty,
                Confidence = 0,
                Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Lines = Array.Empty<string>()
            };
        }

        var endpoint = string.IsNullOrWhiteSpace(options.Endpoint)
            ? "https://api.openai.com/v1/responses"
            : options.Endpoint.Trim();

        var imageBytes = await NormalizeImageAsync(imageStream, cancellationToken);
        if (imageBytes.Length == 0)
        {
            return new CitizenIdOcrResult
            {
                Applied = false,
                Status = "failed",
                Message = "Ảnh đầu vào rỗng.",
                RawText = string.Empty,
                Confidence = 0,
                Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Lines = Array.Empty<string>()
            };
        }

        var ocrModel = ResolveOcrModel(options);
        var ocrCacheKey = BuildCacheKey("ocr", imageBytes, ocrModel);
        if (_cache.TryGetValue<CitizenIdOcrResult>(ocrCacheKey, out var cachedOcr) && cachedOcr is not null)
        {
            return cachedOcr;
        }

        var prompt = "Trich xuat OCR CCCD Viet Nam. "
            + "Chi tra ve JSON hop le theo schema: "
            + "{\"confidence\":number(0..1),\"rawText\":string,\"lines\":string[],\"fields\":{"
            + "\"id_number\":string,\"full_name\":string,\"date_of_birth\":string,\"sex\":string,"
            + "\"nationality\":string,\"place_of_origin\":string,\"place_of_residence\":string,"
            + "\"issue_date\":string,\"expiry_date\":string}}. "
            + "Neu khong thay truong thi de chuoi rong. Khong them markdown, khong giai thich.";

        var payload = await ExecuteOpenAiAsync(endpoint, options, imageBytes, prompt, ocrModel, maxOutputTokens: 700, cancellationToken);
        if (!payload.IsSuccess)
        {
            return new CitizenIdOcrResult
            {
                Applied = false,
                Status = payload.Status,
                Message = payload.Message,
                RawText = string.Empty,
                Confidence = 0,
                Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Lines = Array.Empty<string>()
            };
        }

        if (!TryParseOcr(payload.Text, out var rawText, out var baseConfidence, out var fields, out var lines))
        {
            return new CitizenIdOcrResult
            {
                Applied = false,
                Status = "no_data",
                Message = "OpenAI OCR phản hồi thành công nhưng không parse được JSON kết quả.",
                RawText = string.Empty,
                Confidence = 0,
                Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                Lines = Array.Empty<string>()
            };
        }

        var requiredHits = RequiredOcrKeys.Count(key => fields.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value));
        var requiredCoverage = requiredHits / (double)RequiredOcrKeys.Length;
        var fusedConfidence = Math.Round((baseConfidence * 0.7) + (requiredCoverage * 0.3), 4, MidpointRounding.AwayFromZero);
        var needsReview = fusedConfidence < Math.Clamp(options.MinConfidence, 0.5, 0.99) || requiredHits < RequiredOcrKeys.Length;

        var ocrResult = new CitizenIdOcrResult
        {
            Applied = true,
            Status = needsReview ? "needs_review" : "success",
            Message = needsReview
                ? "OCR CCCD trích xuất được dữ liệu nhưng cần kiểm tra thủ công."
                : "OCR CCCD thành công.",
            RawText = rawText,
            Confidence = fusedConfidence,
            Fields = fields,
            Lines = lines
        };

        _cache.Set(ocrCacheKey, ocrResult, TimeSpan.FromMinutes(5));
        return ocrResult;
    }

    public async Task<CitizenIdDetectAndOcrResult> DetectAndOcrAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        if (!options.Enabled)
        {
            return new CitizenIdDetectAndOcrResult
            {
                Applied = false,
                Status = "disabled",
                Message = "OCR CCCD đang tắt trong cấu hình CitizenIdOpenAi:Enabled.",
                Detect = new CitizenIdSideDetectResponse
                {
                    Side = CitizenIdDetectedSides.Unknown,
                    Confidence = 0,
                    Reasons = new[] { "Nhận diện CCCD đang tắt trong cấu hình CitizenIdOpenAi:Enabled." }
                },
                Ocr = new CitizenIdOcrResult
                {
                    Applied = false,
                    Status = "disabled",
                    Message = "OCR CCCD đang tắt trong cấu hình CitizenIdOpenAi:Enabled.",
                    RawText = string.Empty,
                    Confidence = 0,
                    Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    Lines = Array.Empty<string>()
                }
            };
        }

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return new CitizenIdDetectAndOcrResult
            {
                Applied = false,
                Status = "not_configured",
                Message = "Thiếu API key OpenAI cho nhận diện/OCR CCCD.",
                Detect = new CitizenIdSideDetectResponse
                {
                    Side = CitizenIdDetectedSides.Unknown,
                    Confidence = 0,
                    Reasons = new[] { "Thiếu API key OpenAI cho nhận diện CCCD." }
                },
                Ocr = new CitizenIdOcrResult
                {
                    Applied = false,
                    Status = "not_configured",
                    Message = "Thiếu API key OpenAI cho OCR CCCD.",
                    RawText = string.Empty,
                    Confidence = 0,
                    Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    Lines = Array.Empty<string>()
                }
            };
        }

        var imageBytes = await NormalizeImageAsync(imageStream, cancellationToken);
        if (imageBytes.Length == 0)
        {
            return new CitizenIdDetectAndOcrResult
            {
                Applied = false,
                Status = "failed",
                Message = "Ảnh đầu vào rỗng.",
                Detect = new CitizenIdSideDetectResponse
                {
                    Side = CitizenIdDetectedSides.Unknown,
                    Confidence = 0,
                    Reasons = new[] { "Ảnh đầu vào rỗng." }
                },
                Ocr = new CitizenIdOcrResult
                {
                    Applied = false,
                    Status = "failed",
                    Message = "Ảnh đầu vào rỗng.",
                    RawText = string.Empty,
                    Confidence = 0,
                    Fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase),
                    Lines = Array.Empty<string>()
                }
            };
        }

        var detectModel = ResolveDetectModel(options);
        var ocrModel = ResolveOcrModel(options);
        var combinedCacheKey = BuildCacheKey("combined", imageBytes, $"{detectModel}|{ocrModel}");
        if (_cache.TryGetValue<CitizenIdDetectAndOcrResult>(combinedCacheKey, out var cachedCombined) && cachedCombined is not null)
        {
            return cachedCombined;
        }

        await using var detectStream = new MemoryStream(imageBytes, writable: false);
        await using var ocrStream = new MemoryStream(imageBytes, writable: false);
        var detectTask = DetectSideAsync(detectStream, cancellationToken);
        var ocrTask = ExtractOcrAsync(ocrStream, cancellationToken);
        await Task.WhenAll(detectTask, ocrTask);

        var detect = detectTask.Result;
        var ocr = ocrTask.Result;

        var result = new CitizenIdDetectAndOcrResult
        {
            Applied = ocr.Applied,
            Status = ocr.Status,
            Message = ocr.Applied
                ? "Nhận diện + OCR CCCD thành công."
                : "Nhận diện thành công nhưng OCR chưa áp dụng được.",
            Detect = detect,
            Ocr = ocr
        };

        _cache.Set(combinedCacheKey, result, TimeSpan.FromMinutes(5));
        return result;
    }

    private async Task<OpenAiExecutionResult> ExecuteOpenAiAsync(
        string endpoint,
        CitizenIdOpenAiOptions options,
        byte[] imageBytes,
        string prompt,
        string model,
        int maxOutputTokens,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 3, 45)));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey.Trim());

            var base64Image = Convert.ToBase64String(imageBytes);
            var payload = new
            {
                model = string.IsNullOrWhiteSpace(model) ? "gpt-4.1" : model.Trim(),
                temperature = 0,
                max_output_tokens = Math.Clamp(maxOutputTokens, 120, 1200),
                input = new object[]
                {
                    new
                    {
                        role = "user",
                        content = new object[]
                        {
                            new { type = "input_text", text = prompt },
                            new { type = "input_image", image_url = $"data:image/jpeg;base64,{base64Image}" }
                        }
                    }
                }
            };

            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, linkedCts.Token);
            var body = await response.Content.ReadAsStringAsync(linkedCts.Token);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("CitizenId OpenAI returned non-success status code {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
                return OpenAiExecutionResult.Failed("failed", $"OpenAI trả về mã HTTP {(int)response.StatusCode}.");
            }

            using var document = JsonDocument.Parse(body);
            if (!TryExtractOpenAiText(document.RootElement, out var text) || string.IsNullOrWhiteSpace(text))
            {
                return OpenAiExecutionResult.Failed("no_data", "OpenAI phản hồi thành công nhưng không có output text.");
            }

            return OpenAiExecutionResult.Success(text);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return OpenAiExecutionResult.Failed("timeout", "OpenAI timeout.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CitizenId OpenAI request failed.");
            return OpenAiExecutionResult.Failed("failed", "Không thể gọi OpenAI cho CCCD.");
        }
    }

    private static async Task<byte[]> NormalizeImageAsync(Stream imageStream, CancellationToken cancellationToken)
    {
        imageStream.Position = 0;
        using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
        image.Mutate(ctx =>
        {
            ctx.AutoOrient();

            const int targetMaxDimension = 1800;
            var scale = Math.Min(1d, targetMaxDimension / (double)Math.Max(image.Width, image.Height));
            if (scale < 1d)
            {
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(
                        Math.Max(1, (int)Math.Round(image.Width * scale)),
                        Math.Max(1, (int)Math.Round(image.Height * scale))),
                    Mode = ResizeMode.Max,
                    Sampler = KnownResamplers.Lanczos3
                });
            }

            ctx.Contrast(1.08f);
        });

        using var ms = new MemoryStream();
        await image.SaveAsJpegAsync(ms, new JpegEncoder { Quality = 88 }, cancellationToken);
        return ms.ToArray();
    }

    private static bool TryParseDetectSide(
        string responseText,
        out string side,
        out double confidence,
        out IReadOnlyList<string> reasons)
    {
        side = CitizenIdDetectedSides.Unknown;
        confidence = 0;
        reasons = Array.Empty<string>();

        var json = ExtractFirstJsonObject(responseText);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (TryGetString(root, "side", out var sideValue))
        {
            var normalizedSide = sideValue.Trim().ToLowerInvariant();
            side = normalizedSide is "front" or "back" ? normalizedSide : CitizenIdDetectedSides.Unknown;
        }

        confidence = TryGetDouble(root, "confidence", out var parsedConfidence)
            ? Math.Round(Math.Clamp(parsedConfidence, 0, 1), 4, MidpointRounding.AwayFromZero)
            : 0;

        reasons = ExtractStringArray(root, "reasons");

        if (reasons.Count == 0)
        {
            reasons = new[] { "OpenAI đã phân tích ảnh CCCD." };
        }

        return true;
    }

    private static bool TryParseOcr(
        string responseText,
        out string rawText,
        out double confidence,
        out Dictionary<string, string> fields,
        out IReadOnlyList<string> lines)
    {
        rawText = string.Empty;
        confidence = 0;
        fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        lines = Array.Empty<string>();

        var json = ExtractFirstJsonObject(responseText);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        rawText = TryGetString(root, "rawText", out var raw)
            ? raw
            : TryGetString(root, "raw_text", out var rawSnake) ? rawSnake : string.Empty;

        confidence = TryGetDouble(root, "confidence", out var parsedConfidence)
            ? Math.Clamp(parsedConfidence, 0, 1)
            : 0;

        fields = ExtractStringDictionary(root, "fields");
        lines = ExtractStringArray(root, "lines");

        if (lines.Count == 0 && !string.IsNullOrWhiteSpace(rawText))
        {
            lines = rawText
                .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();
        }

        return !string.IsNullOrWhiteSpace(rawText) || fields.Count > 0 || lines.Count > 0;
    }

    private static string BuildCacheKey(string kind, byte[] imageBytes, string? model)
    {
        var hash = Convert.ToHexString(SHA256.HashData(imageBytes));
        var normalizedModel = string.IsNullOrWhiteSpace(model) ? "gpt-4.1" : model.Trim();
        return $"citizen-openai:{kind}:{normalizedModel}:{hash}";
    }

    private static string ResolveDetectModel(CitizenIdOpenAiOptions options)
    {
        return string.IsNullOrWhiteSpace(options.DetectModel)
            ? "gpt-4.1-mini"
            : options.DetectModel.Trim();
    }

    private static string ResolveOcrModel(CitizenIdOpenAiOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.OcrModel))
        {
            return options.OcrModel.Trim();
        }

        if (!string.IsNullOrWhiteSpace(options.Model))
        {
            return options.Model.Trim();
        }

        return "gpt-4.1";
    }

    private static string ExtractFirstJsonObject(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var start = text.IndexOf('{');
        var end = text.LastIndexOf('}');
        if (start < 0 || end <= start)
        {
            return string.Empty;
        }

        return text[start..(end + 1)];
    }

    private static bool TryExtractOpenAiText(JsonElement root, out string text)
    {
        text = string.Empty;

        if (TryGetString(root, "output_text", out var outputText))
        {
            text = outputText;
            return true;
        }

        if (!TryGetProperty(root, "output", out var outputElement) || outputElement.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var outputItem in outputElement.EnumerateArray())
        {
            if (!TryGetProperty(outputItem, "content", out var contentElement) || contentElement.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in contentElement.EnumerateArray())
            {
                if (TryGetString(contentItem, "text", out var itemText) && !string.IsNullOrWhiteSpace(itemText))
                {
                    text = itemText;
                    return true;
                }
            }
        }

        return false;
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
            return double.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out value)
                || double.TryParse(property.GetString(), NumberStyles.Any, CultureInfo.GetCultureInfo("vi-VN"), out value);
        }

        return false;
    }

    private static bool TryGetBoolean(JsonElement element, string propertyName, out bool value)
    {
        value = false;
        if (!TryGetProperty(element, propertyName, out var property))
        {
            return false;
        }

        if (property.ValueKind == JsonValueKind.True)
        {
            value = true;
            return true;
        }

        if (property.ValueKind == JsonValueKind.False)
        {
            value = false;
            return true;
        }

        if (property.ValueKind == JsonValueKind.String)
        {
            return bool.TryParse(property.GetString(), out value);
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

    private sealed class OpenAiExecutionResult
    {
        public bool IsSuccess { get; init; }

        public string Status { get; init; } = "failed";

        public string Message { get; init; } = string.Empty;

        public string Text { get; init; } = string.Empty;

        public static OpenAiExecutionResult Success(string text)
        {
            return new OpenAiExecutionResult
            {
                IsSuccess = true,
                Status = "success",
                Message = "OK",
                Text = text
            };
        }

        public static OpenAiExecutionResult Failed(string status, string message)
        {
            return new OpenAiExecutionResult
            {
                IsSuccess = false,
                Status = status,
                Message = message,
                Text = string.Empty
            };
        }
    }
}