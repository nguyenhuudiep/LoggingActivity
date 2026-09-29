using System.Globalization;
using System.Text.Json;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Options;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace LoggingActivity.Web.Services;

public sealed class CustomerBehaviorApiException : Exception
{
    public CustomerBehaviorApiException(string message, Exception? innerException = null)
        : base(message, innerException)
    {
    }
}

public sealed class CustomerBehaviorApiClient
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString
    };

    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private readonly IOptions<CustomerBehaviorReportOptions> _options;
    private readonly ILogger<CustomerBehaviorApiClient> _logger;

    public CustomerBehaviorApiClient(
        HttpClient httpClient,
        IMemoryCache cache,
        IOptions<CustomerBehaviorReportOptions> options,
        ILogger<CustomerBehaviorApiClient> logger)
    {
        _httpClient = httpClient;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    // Luôn lấy toàn bộ action/hồ sơ trong khoảng ngày rồi lọc tại chỗ, để cache dùng chung cho mọi bộ lọc
    // và danh sách lựa chọn trên giao diện không bị thu hẹp theo bộ lọc hiện tại.
    public async Task<IReadOnlyList<CustomerApiCallLog>> GetLogsAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var options = _options.Value;
        var cacheKey = $"customer-behavior:{fromDate:yyyy-MM-dd}:{toDate:yyyy-MM-dd}";
        if (_cache.TryGetValue(cacheKey, out IReadOnlyList<CustomerApiCallLog>? cached) && cached is not null)
        {
            return cached;
        }

        var url = QueryHelpers.AddQueryString(options.Endpoint, new Dictionary<string, string?>
        {
            ["fromDate"] = fromDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["toDate"] = toDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ["typeId"] = "-1",
            ["loanBriefId"] = "-1"
        });

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(TimeSpan.FromSeconds(options.TimeoutSeconds <= 0 ? 60 : options.TimeoutSeconds));

        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, timeoutCts.Token);
            await using var stream = await response.Content.ReadAsStreamAsync(timeoutCts.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeoutCts.Token);

            var logs = ParseLogs(document.RootElement, (int)response.StatusCode);
            if (options.CacheSeconds > 0)
            {
                _cache.Set(cacheKey, logs, TimeSpan.FromSeconds(options.CacheSeconds));
            }
            return logs;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new CustomerBehaviorApiException("API LOS phản hồi quá thời gian chờ. Hãy thu hẹp khoảng ngày và thử lại.");
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Customer behavior API returned invalid JSON.");
            throw new CustomerBehaviorApiException("API LOS trả về dữ liệu không đúng định dạng.", ex);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "Customer behavior API request failed.");
            throw new CustomerBehaviorApiException("Không kết nối được tới API LOS.", ex);
        }
    }

    // API LOS có thể trả về mảng trần hoặc bọc trong { meta: { errorCode, errorMessage }, data: [...] }.
    private static IReadOnlyList<CustomerApiCallLog> ParseLogs(JsonElement root, int httpStatusCode)
    {
        if (root.ValueKind == JsonValueKind.Array)
        {
            return Deserialize(root);
        }

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new CustomerBehaviorApiException($"API LOS trả về dữ liệu không hợp lệ (HTTP {httpStatusCode}).");
        }

        if (root.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
        {
            var errorCode = meta.TryGetProperty("errorCode", out var code) && code.TryGetInt32(out var parsedCode) ? parsedCode : 200;
            if (errorCode != 200 && errorCode != 0)
            {
                var errorMessage = meta.TryGetProperty("errorMessage", out var message) ? message.GetString() : null;
                throw new CustomerBehaviorApiException($"API LOS báo lỗi {errorCode}: {errorMessage ?? "không rõ nguyên nhân"}.");
            }
        }

        if (httpStatusCode >= 400)
        {
            throw new CustomerBehaviorApiException($"API LOS trả về HTTP {httpStatusCode}.");
        }

        if (!root.TryGetProperty("data", out var data) || data.ValueKind == JsonValueKind.Null)
        {
            return Array.Empty<CustomerApiCallLog>();
        }

        if (data.ValueKind == JsonValueKind.Array)
        {
            return Deserialize(data);
        }

        if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
        {
            return Deserialize(items);
        }

        throw new CustomerBehaviorApiException("API LOS trả về trường data không phải danh sách.");
    }

    private static IReadOnlyList<CustomerApiCallLog> Deserialize(JsonElement array)
    {
        return array.Deserialize<List<CustomerApiCallLog>>(SerializerOptions) ?? new List<CustomerApiCallLog>();
    }
}
