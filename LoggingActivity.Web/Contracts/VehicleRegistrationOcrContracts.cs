using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LoggingActivity.Web.Contracts;

public sealed class VehicleRegistrationOcrRequest
{
    [Required]
    public IFormFile? Image { get; set; }

    public string? OpenAiApiKey { get; set; }
}

public sealed class VehicleRegistrationOcrResult
{
    public bool Applied { get; init; }

    public bool NeedsReview { get; init; }

    public string Status { get; init; } = "no_data";

    public string Message { get; init; } = string.Empty;

    public string Provider { get; init; } = string.Empty;

    public string RawText { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
}

public sealed class VehicleRegistrationOcrCompareItem
{
    public string Provider { get; init; } = string.Empty;

    public VehicleRegistrationOcrResult Result { get; init; } = new();
}

public sealed class VehicleRegistrationOcrCompareResult
{
    public bool AppliedAny { get; init; }

    public int SuccessCount { get; init; }

    public IReadOnlyList<VehicleRegistrationOcrCompareItem> Results { get; init; } = Array.Empty<VehicleRegistrationOcrCompareItem>();
}