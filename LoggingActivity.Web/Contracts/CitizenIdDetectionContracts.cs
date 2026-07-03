using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LoggingActivity.Web.Contracts;

public sealed class CitizenIdSideDetectRequest
{
    [Required]
    public IFormFile? Image { get; set; }

    public bool IncludeOcr { get; set; } = true;
}

public sealed class CitizenIdSideDetectResponse
{
    public string Side { get; init; } = CitizenIdDetectedSides.Unknown;

    public double Confidence { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();

    public CitizenIdSideDetectSignals Signals { get; init; } = new();

    public CitizenIdOcrResult Ocr { get; init; } = new();
}

public sealed class CitizenIdOcrResult
{
    public bool Requested { get; init; }

    public bool Applied { get; init; }

    public string Status { get; init; } = "disabled";

    public string Message { get; init; } = string.Empty;

    public string RawText { get; init; } = string.Empty;

    public double Confidence { get; init; }

    public IReadOnlyDictionary<string, string> Fields { get; init; } = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<string> Lines { get; init; } = Array.Empty<string>();
}

public sealed class CitizenIdSideDetectSignals
{
    public bool QrDetected { get; init; }

    public bool BarcodeDetected { get; init; }

    public bool PortraitLikeDetected { get; init; }

    public bool EmblemLikeDetected { get; init; }

    public bool FrontPhotoLayoutLike { get; init; }

    public bool TextHeavyBothSides { get; init; }

    public bool UniformTextDistribution { get; init; }

    public bool StructuralBackLayoutLike { get; init; }

    public bool QrReliable { get; init; }

    public bool MrzReliable { get; init; }

    public bool LikelyCitizenId { get; init; }

    public int FrontSignalCount { get; init; }

    public int BackSignalCount { get; init; }

    public int StrongFrontSignalCount { get; init; }

    public int StrongBackSignalCount { get; init; }

    public double CenterSkinRatio { get; init; }

    public double LeftSkinRatio { get; init; }

    public double RightSkinRatio { get; init; }

    public double MrzBandStrength { get; init; }

    public double BackRegionInkDensity { get; init; }

    public double MidLeftInkDensity { get; init; }

    public double MidRightInkDensity { get; init; }

    public double TopBandInkDensity { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }
}

public static class CitizenIdDetectedSides
{
    public const string Front = "front";
    public const string Back = "back";
    public const string Unknown = "unknown";
}
