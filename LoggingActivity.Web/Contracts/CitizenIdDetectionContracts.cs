using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace LoggingActivity.Web.Contracts;

public sealed class CitizenIdSideDetectRequest
{
    [Required]
    public IFormFile? Image { get; set; }

    public string? FileNameHint { get; set; }
}

public sealed class CitizenIdSideDetectResponse
{
    public string Side { get; init; } = CitizenIdDetectedSides.Unknown;

    public double Confidence { get; init; }

    public IReadOnlyList<string> Reasons { get; init; } = Array.Empty<string>();

    public CitizenIdSideDetectSignals Signals { get; init; } = new();
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

    public string? ImageHintMatched { get; init; }

    public int Width { get; init; }

    public int Height { get; init; }
}

public static class CitizenIdDetectedSides
{
    public const string Front = "front";
    public const string Back = "back";
    public const string Unknown = "unknown";
}
