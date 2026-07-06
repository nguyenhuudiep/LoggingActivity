using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using LoggingActivity.Web.Contracts;
using LoggingActivity.Web.Options;
using Microsoft.Extensions.Options;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LoggingActivity.Web.Services;

public sealed partial class VehicleRegistrationOcrService
{
    private const string InvalidDocumentMessage = "Ảnh không đúng loại giấy tờ yêu cầu.";

    private static readonly string[] RequiredKeys =
    {
        "license_plate",
        "engine_number",
        "chassis_number"
    };

    private readonly HttpClient _httpClient;
    private readonly IOptionsMonitor<VehicleRegistrationOcrOptions> _options;
    private readonly ILogger<VehicleRegistrationOcrService> _logger;

    public VehicleRegistrationOcrService(
        HttpClient httpClient,
        IOptionsMonitor<VehicleRegistrationOcrOptions> options,
        ILogger<VehicleRegistrationOcrService> logger)
    {
        _httpClient = httpClient;
        _options = options;
        _logger = logger;
    }

    public async Task<VehicleRegistrationOcrResult> ExtractAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        return await ExtractAsync(imageStream, providerOverride: null, openAiApiKeyOverride: null, cancellationToken);
    }

    public async Task<VehicleRegistrationOcrResult> ExtractAsync(
        Stream imageStream,
        string? providerOverride,
        CancellationToken cancellationToken = default)
    {
        return await ExtractAsync(imageStream, providerOverride, openAiApiKeyOverride: null, cancellationToken);
    }

    public async Task<VehicleRegistrationOcrResult> ExtractAsync(
        Stream imageStream,
        string? providerOverride,
        string? openAiApiKeyOverride,
        CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;

        var provider = NormalizeProvider(providerOverride ?? options.Provider);

        if (!options.Enabled)
        {
            return BuildDisabled(provider, "OCR giấy đăng ký xe đang tắt trong cấu hình VehicleRegistrationOcr:Enabled.");
        }

        var providerConfig = ResolveProviderConfiguration(provider, options, openAiApiKeyOverride);
        if (string.IsNullOrWhiteSpace(providerConfig.Endpoint))
        {
            var message = provider == "openai"
                ? "Thiếu endpoint cho OpenAI OCR. Cấu hình VehicleRegistrationOcr:OpenAiEndpoint hoặc VehicleRegistrationOcr:Endpoint."
                : "Thiếu endpoint cho Custom OCR. Cấu hình VehicleRegistrationOcr:CustomEndpoint.";
            return BuildNotConfigured(provider, message);
        }

        if (provider == "openai" && string.IsNullOrWhiteSpace(providerConfig.ApiKey))
        {
            var message = "Thiếu API key cho OpenAI OCR. Cấu hình VehicleRegistrationOcr:OpenAiApiKey hoặc VehicleRegistrationOcr:ApiKey.";
            return BuildNotConfigured(provider, message);
        }

        if (imageStream is null)
        {
            return BuildFailed(provider, "Ảnh đầu vào rỗng.");
        }

        byte[] imageBytes;
        try
        {
            imageBytes = await NormalizeImageAsync(imageStream, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Khong the xu ly anh OCR dang ky xe.");
            return BuildFailed(provider, "Không thể xử lý ảnh đầu vào. Vui lòng thử ảnh rõ hơn.");
        }

        if (imageBytes.Length == 0)
        {
            return BuildFailed(provider, "Ảnh đầu vào rỗng.");
        }

        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(Math.Clamp(options.TimeoutSeconds, 3, 45)));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        VehicleOcrExecution execution;
        try
        {
            execution = provider == "openai"
                ? await ExecuteOpenAiAsync(imageBytes, providerConfig, linkedCts.Token)
                : await ExecuteCustomEndpointAsync(imageBytes, providerConfig, linkedCts.Token);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning("Vehicle registration OCR timed out.");
            return BuildTimeout(provider, "OCR endpoint timeout.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vehicle registration OCR failed.");
            return BuildFailed(provider, "Không thể gọi OCR endpoint. Vui lòng kiểm tra cấu hình và thử lại.");
        }

        if (!execution.IsSuccess || execution.Prediction is null)
        {
            return new VehicleRegistrationOcrResult
            {
                Applied = false,
                NeedsReview = true,
                Status = execution.Status,
                Message = execution.Message,
                Provider = provider,
                RawText = string.Empty,
                Confidence = 0,
                Fields = BuildDefaultFields(),
                Lines = Array.Empty<string>()
            };
        }

        var normalizedFields = NormalizeFields(execution.Prediction.Fields, execution.Prediction.RawText, execution.Prediction.Lines);
        var lines = execution.Prediction.Lines.Count > 0
            ? execution.Prediction.Lines
            : SplitLines(execution.Prediction.RawText);

        var requiredHits = RequiredKeys.Count(key => normalizedFields.ContainsKey(key) && !string.IsNullOrWhiteSpace(normalizedFields[key]));
        var requiredCoverage = RequiredKeys.Length == 0 ? 0 : requiredHits / (double)RequiredKeys.Length;
        var baseConfidence = Math.Clamp(execution.Prediction.Confidence, 0, 1);
        var fusedConfidence = Math.Round((baseConfidence * 0.7) + (requiredCoverage * 0.3), 4, MidpointRounding.AwayFromZero);
        var needsReview = fusedConfidence < Math.Clamp(options.MinConfidence, 0.5, 0.99) || requiredHits < RequiredKeys.Length;

        return new VehicleRegistrationOcrResult
        {
            Applied = true,
            NeedsReview = needsReview,
            Status = needsReview ? "needs_review" : "success",
            Message = needsReview
                ? "OCR trích xuất được dữ liệu nhưng cần kiểm tra thủ công trước khi dùng nghiệp vụ."
                : "OCR giấy đăng ký xe thành công.",
            Provider = provider,
            RawText = execution.Prediction.RawText,
            Confidence = fusedConfidence,
            Fields = normalizedFields,
            Lines = lines
        };
    }

    private async Task<VehicleOcrExecution> ExecuteOpenAiAsync(
        byte[] imageBytes,
        ProviderConfiguration providerConfig,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, providerConfig.Endpoint.Trim());
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", providerConfig.ApiKey.Trim());

        var base64Image = Convert.ToBase64String(imageBytes);
        var prompt = "Trich xuat OCR giay dang ky xe o to Viet Nam. Chi tra ve JSON object hop le voi schema: "
            + "{\"is_vehicle_registration\": boolean,"
            + " \"confidence\": number(0..1), \"rawText\": string, \"lines\": string[], \"fields\": {"
            + "\"license_plate\": string, \"registration_number\": string, \"owner_name\": string, \"owner_address\": string, "
            + "\"vehicle_brand\": string, \"vehicle_type\": string, \"engine_number\": string, \"chassis_number\": string, "
            + "\"color\": string, \"seat_count\": string, \"issue_date\": string, \"expiry_date\": string}}. "
            + "Neu anh khong phai giay dang ky xe o to Viet Nam thi dat is_vehicle_registration=false"
            + " va de rawText rong, lines rong, fields rong."
            + " Neu khong thay truong thi de chuoi rong. Khong them markdown, khong giai thich.";

        var payload = new
        {
            model = providerConfig.Model,
            temperature = 0,
            max_output_tokens = 820,
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

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("OpenAI vehicle OCR returned non-success status code {StatusCode}. Body: {Body}", (int)response.StatusCode, body);
            return VehicleOcrExecution.Failed($"OpenAI OCR trả về mã HTTP {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(body);
        if (!TryExtractOpenAiText(document.RootElement, out var responseText) || string.IsNullOrWhiteSpace(responseText))
        {
            return VehicleOcrExecution.NoData("OpenAI OCR phản hồi thành công nhưng không có output text.");
        }

        if (!TryParseModelPrediction(responseText, out var prediction, out var isVehicleRegistration))
        {
            return VehicleOcrExecution.Failed("Không parse được JSON OCR từ OpenAI response.");
        }

        if (!isVehicleRegistration)
        {
            return VehicleOcrExecution.InvalidDocument(InvalidDocumentMessage);
        }

        return VehicleOcrExecution.Success(prediction);
    }

    private async Task<VehicleOcrExecution> ExecuteCustomEndpointAsync(
        byte[] imageBytes,
        ProviderConfiguration providerConfig,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, providerConfig.Endpoint.Trim());
        if (!string.IsNullOrWhiteSpace(providerConfig.ApiKey))
        {
            var headerName = providerConfig.ApiKeyHeaderName;
            request.Headers.Remove(headerName);
            request.Headers.Add(headerName, providerConfig.ApiKey.Trim());
        }

        using var content = new MultipartFormDataContent();
        var imageContent = new ByteArrayContent(imageBytes);
        imageContent.Headers.ContentType = MediaTypeHeaderValue.Parse("image/jpeg");
        content.Add(imageContent, "image", "vehicle-registration.jpg");
        request.Content = content;

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            _logger.LogWarning("Vehicle OCR custom endpoint returned non-success status code {StatusCode}.", (int)response.StatusCode);
            return VehicleOcrExecution.Failed($"OCR endpoint trả về mã HTTP {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var root = document.RootElement;
        if (TryGetBoolean(root, "is_vehicle_registration", out var isVehicleRegistration) && !isVehicleRegistration)
        {
            return VehicleOcrExecution.InvalidDocument(InvalidDocumentMessage);
        }

        if (TryGetString(root, "status", out var statusValue)
            && string.Equals(statusValue, "invalid_document", StringComparison.OrdinalIgnoreCase))
        {
            return VehicleOcrExecution.InvalidDocument(InvalidDocumentMessage);
        }

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
            return VehicleOcrExecution.NoData("OCR endpoint phản hồi thành công nhưng không có dữ liệu text/fields/lines.");
        }

        return VehicleOcrExecution.Success(new VehicleOcrPrediction
        {
            RawText = rawText,
            Confidence = confidence,
            Fields = fields,
            Lines = lines
        });
    }

    private static async Task<byte[]> NormalizeImageAsync(Stream imageStream, CancellationToken cancellationToken)
    {
        imageStream.Position = 0;
        using var image = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);
        image.Mutate(ctx =>
        {
            ctx.AutoOrient();

            const int targetMaxDimension = 2200;
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
        await image.SaveAsJpegAsync(ms, new JpegEncoder
        {
            Quality = 92
        }, cancellationToken);
        return ms.ToArray();
    }

    private static Dictionary<string, string> NormalizeFields(
        IReadOnlyDictionary<string, string> rawFields,
        string rawText,
        IReadOnlyList<string> lines)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in rawFields)
        {
            if (string.IsNullOrWhiteSpace(field.Key) || string.IsNullOrWhiteSpace(field.Value))
            {
                continue;
            }

            map[field.Key.Trim()] = field.Value.Trim();
        }

        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["license_plate"] = FirstNonEmpty(map, "license_plate", "licensePlate", "bien_so", "bien_kiem_soat"),
            ["registration_number"] = FirstNonEmpty(map, "registration_number", "registrationNumber", "so_dang_ky", "so_dk"),
            ["owner_name"] = FirstNonEmpty(map, "owner_name", "ownerName", "chu_xe"),
            ["owner_address"] = FirstNonEmpty(map, "owner_address", "ownerAddress", "dia_chi"),
            ["vehicle_brand"] = FirstNonEmpty(map, "vehicle_brand", "vehicleBrand", "nhan_hieu"),
            ["vehicle_type"] = FirstNonEmpty(map, "vehicle_type", "vehicleType", "loai_xe"),
            ["engine_number"] = FirstNonEmpty(map, "engine_number", "engineNumber", "so_may"),
            ["chassis_number"] = FirstNonEmpty(map, "chassis_number", "chassisNumber", "so_khung"),
            ["color"] = FirstNonEmpty(map, "color", "mau_son"),
            ["seat_count"] = FirstNonEmpty(map, "seat_count", "seatCount", "so_cho_ngoi"),
            ["issue_date"] = FirstNonEmpty(map, "issue_date", "issueDate", "ngay_cap"),
            ["expiry_date"] = FirstNonEmpty(map, "expiry_date", "expiryDate", "valid_until", "validTo", "ngay_het_han", "co_gia_tri_den")
        };

        var candidateText = BuildSearchText(rawText, lines);

        if (string.IsNullOrWhiteSpace(normalized["license_plate"]))
        {
            normalized["license_plate"] = ExtractLicensePlate(candidateText);
        }

        if (string.IsNullOrWhiteSpace(normalized["engine_number"]))
        {
            normalized["engine_number"] = ExtractByKeyword(lines, "so may", "số máy", "engine");
        }

        if (string.IsNullOrWhiteSpace(normalized["chassis_number"]))
        {
            normalized["chassis_number"] = ExtractByKeyword(lines, "so khung", "số khung", "chassis", "vin");
        }

        if (string.IsNullOrWhiteSpace(normalized["expiry_date"]))
        {
            normalized["expiry_date"] = ExtractDateByKeyword(lines, "co gia tri den", "có giá trị đến", "ngay het han", "ngày hết hạn", "valid until", "expire");
        }

        normalized["license_plate"] = NormalizePlate(normalized["license_plate"]);
        normalized["engine_number"] = NormalizeSerial(normalized["engine_number"]);
        normalized["chassis_number"] = NormalizeSerial(normalized["chassis_number"]);
        normalized["issue_date"] = NormalizeDate(normalized["issue_date"]);
        normalized["expiry_date"] = NormalizeDate(normalized["expiry_date"]);

        var cleaned = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in normalized)
        {
            if (string.IsNullOrWhiteSpace(item.Value))
            {
                continue;
            }

            cleaned[item.Key] = item.Value.Trim();
        }

        if (!cleaned.ContainsKey("expiry_date"))
        {
            cleaned["expiry_date"] = string.Empty;
        }

        return cleaned;
    }

    private static string BuildSearchText(string rawText, IReadOnlyList<string> lines)
    {
        if (!string.IsNullOrWhiteSpace(rawText))
        {
            return rawText;
        }

        return string.Join('\n', lines);
    }

    private static IReadOnlyList<string> SplitLines(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
        {
            return Array.Empty<string>();
        }

        var lines = rawText
            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        return lines.Length == 0 ? new[] { rawText } : lines;
    }

    private static string ExtractLicensePlate(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var match = LicensePlateRegex().Match(text.ToUpperInvariant());
        return match.Success ? match.Value : string.Empty;
    }

    private static string ExtractByKeyword(IReadOnlyList<string> lines, params string[] keywords)
    {
        if (lines.Count == 0 || keywords.Length == 0)
        {
            return string.Empty;
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var normalized = line.Trim();
            var contains = keywords.Any(keyword => normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (!contains)
            {
                continue;
            }

            var separatorIndex = normalized.IndexOf(':');
            if (separatorIndex >= 0 && separatorIndex < normalized.Length - 1)
            {
                return normalized[(separatorIndex + 1)..].Trim();
            }

            var pieces = normalized.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            return pieces.Length > 0 ? pieces[^1] : string.Empty;
        }

        return string.Empty;
    }

    private static string ExtractDateByKeyword(IReadOnlyList<string> lines, params string[] keywords)
    {
        if (lines.Count == 0 || keywords.Length == 0)
        {
            return string.Empty;
        }

        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var normalized = line.Trim();
            var contains = keywords.Any(keyword => normalized.Contains(keyword, StringComparison.OrdinalIgnoreCase));
            if (!contains)
            {
                continue;
            }

            var match = DateValueRegex().Match(normalized);
            if (match.Success)
            {
                return match.Value;
            }
        }

        return string.Empty;
    }

    private static string FirstNonEmpty(IReadOnlyDictionary<string, string> values, params string[] keys)
    {
        foreach (var key in keys)
        {
            if (!values.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            return value.Trim();
        }

        return string.Empty;
    }

    private static ProviderConfiguration ResolveProviderConfiguration(
        string provider,
        VehicleRegistrationOcrOptions options,
        string? openAiApiKeyOverride)
    {
        if (provider == "custom")
        {
            return new ProviderConfiguration
            {
                Endpoint = PickFirstNonEmpty(options.CustomEndpoint),
                ApiKey = PickFirstNonEmpty(options.CustomApiKey, options.ApiKey),
                ApiKeyHeaderName = PickFirstNonEmpty(options.CustomApiKeyHeaderName, options.ApiKeyHeaderName, "X-Api-Key"),
                Model = PickFirstNonEmpty(options.Model, "gpt-4.1")
            };
        }

        return new ProviderConfiguration
        {
            Endpoint = PickFirstNonEmpty(options.OpenAiEndpoint, options.Endpoint, "https://api.openai.com/v1/responses"),
            ApiKey = PickFirstNonEmpty(openAiApiKeyOverride ?? string.Empty, options.OpenAiApiKey, options.ApiKey),
            ApiKeyHeaderName = "Authorization",
            Model = PickFirstNonEmpty(options.OpenAiModel, options.Model, "gpt-4.1")
        };
    }

    private static string PickFirstNonEmpty(params string[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }

    private static string NormalizeProvider(string provider)
    {
        var normalized = string.IsNullOrWhiteSpace(provider) ? "openai" : provider.Trim().ToLowerInvariant();
        return normalized is "openai" or "custom" ? normalized : "openai";
    }

    private static string NormalizePlate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var normalized = value.ToUpperInvariant().Replace(" ", string.Empty);
        normalized = normalized.Replace('O', '0');
        return normalized;
    }

    private static string NormalizeSerial(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var chars = value
            .Where(ch => char.IsLetterOrDigit(ch))
            .Select(char.ToUpperInvariant)
            .ToArray();
        return new string(chars);
    }

    private static string NormalizeDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        if (DateTime.TryParse(value, CultureInfo.GetCultureInfo("vi-VN"), DateTimeStyles.None, out var parsed)
            || DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed))
        {
            return parsed.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        return value.Trim();
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

    private static bool TryParseModelPrediction(
        string responseText,
        out VehicleOcrPrediction prediction,
        out bool isVehicleRegistration)
    {
        prediction = new VehicleOcrPrediction();
        isVehicleRegistration = true;
        var hasDocumentDecision = false;

        var json = ExtractFirstJsonObject(responseText);
        if (string.IsNullOrWhiteSpace(json))
        {
            return false;
        }

        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (TryGetBoolean(root, "is_vehicle_registration", out var parsedIsVehicleRegistration))
        {
            isVehicleRegistration = parsedIsVehicleRegistration;
            hasDocumentDecision = true;
        }

        var confidence = TryGetDouble(root, "confidence", out var parsedConfidence)
            ? Math.Clamp(parsedConfidence, 0, 1)
            : 0;
        var rawText = TryGetString(root, "rawText", out var raw)
            ? raw
            : TryGetString(root, "raw_text", out var rawSnake) ? rawSnake : string.Empty;
        var fields = ExtractStringDictionary(root, "fields");
        var lines = ExtractStringArray(root, "lines");

        if (!hasDocumentDecision && string.IsNullOrWhiteSpace(rawText) && fields.Count == 0 && lines.Count == 0)
        {
            return false;
        }

        prediction = new VehicleOcrPrediction
        {
            RawText = rawText,
            Confidence = confidence,
            Fields = fields,
            Lines = lines
        };

        return true;
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

    private static VehicleRegistrationOcrResult BuildDisabled(string provider, string message)
    {
        return new VehicleRegistrationOcrResult
        {
            Applied = false,
            NeedsReview = true,
            Status = "disabled",
            Message = message,
            Provider = provider,
            Fields = BuildDefaultFields()
        };
    }

    private static VehicleRegistrationOcrResult BuildNotConfigured(string provider, string message)
    {
        return new VehicleRegistrationOcrResult
        {
            Applied = false,
            NeedsReview = true,
            Status = "not_configured",
            Message = message,
            Provider = provider,
            Fields = BuildDefaultFields()
        };
    }

    private static VehicleRegistrationOcrResult BuildTimeout(string provider, string message)
    {
        return new VehicleRegistrationOcrResult
        {
            Applied = false,
            NeedsReview = true,
            Status = "timeout",
            Message = message,
            Provider = provider,
            Fields = BuildDefaultFields()
        };
    }

    private static VehicleRegistrationOcrResult BuildFailed(string provider, string message)
    {
        return new VehicleRegistrationOcrResult
        {
            Applied = false,
            NeedsReview = true,
            Status = "failed",
            Message = message,
            Provider = provider,
            Fields = BuildDefaultFields()
        };
    }

    private static IReadOnlyDictionary<string, string> BuildDefaultFields()
    {
        return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["expiry_date"] = string.Empty
        };
    }

    [GeneratedRegex(@"\b\d{2}[A-Z]{1,2}-?\d{3}[\.]?\d{2}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex LicensePlateRegex();

    [GeneratedRegex(@"\b\d{1,2}[\./-]\d{1,2}[\./-]\d{2,4}\b", RegexOptions.Compiled | RegexOptions.CultureInvariant)]
    private static partial Regex DateValueRegex();

    private sealed class VehicleOcrPrediction
    {
        public string RawText { get; init; } = string.Empty;

        public double Confidence { get; init; }

        public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
    }

    private sealed class VehicleOcrExecution
    {
        public bool IsSuccess { get; init; }

        public string Status { get; init; } = "failed";

        public string Message { get; init; } = string.Empty;

        public VehicleOcrPrediction? Prediction { get; init; }

        public static VehicleOcrExecution Success(VehicleOcrPrediction prediction)
        {
            return new VehicleOcrExecution
            {
                IsSuccess = true,
                Status = "success",
                Message = "OCR thành công.",
                Prediction = prediction
            };
        }

        public static VehicleOcrExecution NoData(string message)
        {
            return new VehicleOcrExecution
            {
                IsSuccess = false,
                Status = "no_data",
                Message = message
            };
        }

        public static VehicleOcrExecution Failed(string message)
        {
            return new VehicleOcrExecution
            {
                IsSuccess = false,
                Status = "failed",
                Message = message
            };
        }

        public static VehicleOcrExecution InvalidDocument(string message)
        {
            return new VehicleOcrExecution
            {
                IsSuccess = false,
                Status = "invalid_document",
                Message = message
            };
        }
    }

    private sealed class ProviderConfiguration
    {
        public string Endpoint { get; init; } = string.Empty;

        public string ApiKey { get; init; } = string.Empty;

        public string ApiKeyHeaderName { get; init; } = string.Empty;

        public string Model { get; init; } = string.Empty;
    }
}