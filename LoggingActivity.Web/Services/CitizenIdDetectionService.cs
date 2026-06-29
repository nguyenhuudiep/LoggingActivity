using LoggingActivity.Web.Contracts;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace LoggingActivity.Web.Services;

public sealed class CitizenIdDetectionService
{
    public async Task<CitizenIdSideDetectResponse> DetectSideAsync(Stream imageStream, CancellationToken cancellationToken = default)
    {
        using var sourceImage = await Image.LoadAsync<Rgba32>(imageStream, cancellationToken);

        var reasons = new List<string>();
        using var image = CreateWorkingImage(sourceImage, reasons);
        var frontScore = 0.0;
        var backScore = 0.0;

        var adaptiveInkThreshold = ComputeAdaptiveInkThreshold(image);

        var qrDetected = DetectQrLikePattern(image, adaptiveInkThreshold);
        var barcodeDetected = qrDetected;
        var leftSkinRatio = ComputeSkinRatio(image, 0.08, 0.46, 0.22, 0.80);
        var rightSkinRatio = ComputeSkinRatio(image, 0.54, 0.94, 0.22, 0.80);
        var centerSkinRatio = ComputeSkinRatio(image, 0.30, 0.70, 0.22, 0.80);
        var mrzBandStrength = EstimateMrzBandStrength(image, adaptiveInkThreshold);
        var backRegionInkDensity = ComputeInkDensity(image, 0.52, 0.98, 0.42, 0.95, adaptiveInkThreshold);
        var midLeftInkDensity = ComputeInkDensity(image, 0.05, 0.47, 0.23, 0.78, adaptiveInkThreshold);
        var midRightInkDensity = ComputeInkDensity(image, 0.53, 0.95, 0.23, 0.78, adaptiveInkThreshold);
        var topBandInkDensity = ComputeInkDensity(image, 0.05, 0.95, 0.05, 0.20, adaptiveInkThreshold);
        var emblemLikeDetected = DetectNationalEmblemLike(image);
        var portraitInkCompatible = midLeftInkDensity < 0.22;
        var portraitLikeDetected = leftSkinRatio is >= 0.065 and <= 0.24
            && (leftSkinRatio > (rightSkinRatio * 1.15) || centerSkinRatio >= 0.15)
            && portraitInkCompatible;
        var frontPhotoLayoutLike = midLeftInkDensity < 0.17 && midRightInkDensity > (midLeftInkDensity + 0.04);
        var textHeavyBothSides = midLeftInkDensity > 0.2 && midRightInkDensity > 0.2;
        var uniformTextDistribution = Math.Abs(midLeftInkDensity - midRightInkDensity) < 0.055;
        var lowDocumentTextDensity = topBandInkDensity < 0.045 && midLeftInkDensity < 0.14 && midRightInkDensity < 0.14;
        var asymmetricTextLayout = Math.Abs(midLeftInkDensity - midRightInkDensity) > 0.22
            && Math.Max(midLeftInkDensity, midRightInkDensity) > 0.22
            && Math.Min(midLeftInkDensity, midRightInkDensity) < 0.08;
        var structuralBackLayoutLike = backRegionInkDensity >= 0.3
            && asymmetricTextLayout
            && centerSkinRatio < 0.12
            && !frontPhotoLayoutLike
            && !emblemLikeDetected;
        var qrReliable = qrDetected && (backRegionInkDensity >= 0.2 || midRightInkDensity >= 0.16);
        var mrzReliable = mrzBandStrength >= 0.28
            && (backRegionInkDensity >= 0.2 || textHeavyBothSides || uniformTextDistribution || midRightInkDensity >= 0.16);

        if (qrDetected)
        {
            if (qrReliable)
            {
                backScore += 0.45;
                reasons.Add("Phát hiện QR code đáng tin cậy, thường xuất hiện ở mặt sau CCCD.");
            }
            else
            {
                backScore += 0.08;
                reasons.Add("Có tín hiệu QR nhưng chưa đủ chỉ dấu phụ để kết luận mạnh mặt sau.");
            }
        }

        if (barcodeDetected && !qrDetected)
        {
            backScore += 0.2;
            reasons.Add("Phát hiện barcode nhưng không phải QR, ưu tiên mặt sau.");
        }

        var frontHintMatched = false;
        var backHintMatched = false;

        if (portraitLikeDetected)
        {
            frontScore += 0.65;
            reasons.Add("Phát hiện vùng da lệch trái giống vùng chân dung, ưu tiên mặt trước.");
        }

        if (centerSkinRatio >= 0.16)
        {
            frontScore += 0.34;
            reasons.Add("Vùng da trung tâm cao, nghiêng về bố cục mặt trước có ảnh chân dung.");
        }

        if (emblemLikeDetected)
        {
            frontScore += 0.46;
            reasons.Add("Phát hiện cụm màu nóng vùng góc trái trên giống quốc huy, ưu tiên mặt trước.");
        }

        if (frontPhotoLayoutLike)
        {
            frontScore += 0.62;
            reasons.Add("Bố cục trái-thưa/phải-dày giống vùng ảnh chân dung + vùng text mặt trước.");
        }

        var allowSkinBoost = (!qrDetected || emblemLikeDetected)
            && mrzBandStrength < 0.25
            && !textHeavyBothSides
            && midLeftInkDensity < 0.2;
        if (allowSkinBoost && centerSkinRatio >= 0.09)
        {
            frontScore += 0.3;
            reasons.Add("Tỷ lệ vùng da ở trung tâm cao, có khả năng là ảnh mặt trước.");
        }
        else if (allowSkinBoost && centerSkinRatio >= 0.05)
        {
            frontScore += 0.12;
            reasons.Add("Có tín hiệu vùng da mức trung bình ở trung tâm ảnh.");
        }

        if (mrzBandStrength >= 0.34)
        {
            if (!mrzReliable)
            {
                backScore += 0.08;
                reasons.Add("Có tín hiệu MRZ-like nhưng thiếu chỉ dấu phụ, giảm trọng số để tránh false positive.");
            }
            else if (lowDocumentTextDensity)
            {
                backScore += 0.2;
                reasons.Add("Có tín hiệu MRZ-like nhưng mật độ chữ tổng thể thấp, giảm ưu tiên mặt sau để tránh nhiễu nền.");
            }
            else
            {
                backScore += 0.65;
                reasons.Add("Vùng đáy có cấu trúc dải ký tự dày (MRZ-like), ưu tiên mặt sau.");
            }
        }
        else if (mrzBandStrength >= 0.25)
        {
            if (!mrzReliable)
            {
                backScore += 0.04;
                reasons.Add("Tín hiệu MRZ-like mức trung bình nhưng chưa đủ độ tin cậy.");
            }
            else if (lowDocumentTextDensity)
            {
                backScore += 0.1;
                reasons.Add("Vùng đáy có tín hiệu ký tự mức trung bình nhưng tổng thể ít chữ, giảm ưu tiên mặt sau.");
            }
            else
            {
                backScore += 0.35;
                reasons.Add("Vùng đáy có mật độ ký tự tương đối cao, nghiêng về mặt sau.");
            }
        }

        if (backRegionInkDensity >= 0.3)
        {
            backScore += 0.2;
            reasons.Add("Nửa phải ảnh có mật độ text/ink cao, phù hợp bố cục mặt sau.");
        }

        if (textHeavyBothSides)
        {
            backScore += 0.33;
            reasons.Add("Cả hai nửa trái/phải đều có mật độ text cao, nghiêng về mặt sau.");
        }

        if (!qrReliable && !mrzReliable && centerSkinRatio >= 0.28)
        {
            backScore -= 0.28;
            reasons.Add("Mật độ da trung tâm cao nhưng thiếu tín hiệu back đặc thù (QR/MRZ đáng tin), giảm ưu tiên mặt sau.");
        }

        if (asymmetricTextLayout && !frontPhotoLayoutLike && !emblemLikeDetected)
        {
            backScore += 0.3;
            reasons.Add("Bố cục chữ lệch mạnh một phía và không giống bố cục ảnh chân dung mặt trước, tăng ưu tiên mặt sau.");
        }

        if (structuralBackLayoutLike)
        {
            backScore += 0.26;
            reasons.Add("Bố cục vùng phải + phân bố chữ lệch phù hợp mẫu mặt sau không QR/MRZ.");
        }

        if (uniformTextDistribution && topBandInkDensity > 0.18)
        {
            backScore += 0.16;
            reasons.Add("Phân bố text khá đều toàn thẻ, phù hợp mặt sau nhiều trường thông tin.");
        }

        if (!portraitLikeDetected && mrzBandStrength >= 0.22 && midLeftInkDensity >= 0.16 && midRightInkDensity >= 0.16)
        {
            backScore += 0.24;
            reasons.Add("Không có dấu hiệu chân dung và mật độ text đồng đều ở vùng giữa, tăng ưu tiên mặt sau.");
        }

        if (qrDetected && (portraitLikeDetected || emblemLikeDetected))
        {
            backScore -= 0.26;
            reasons.Add("Tín hiệu QR xung đột với dấu hiệu mặt trước rõ, giảm trọng số QR để tránh nhận sai.");
        }

        if (mrzBandStrength >= 0.3 && centerSkinRatio < 0.05)
        {
            backScore += 0.12;
        }

        if (mrzBandStrength >= 0.3 && centerSkinRatio >= 0.16 && lowDocumentTextDensity)
        {
            backScore -= 0.24;
            reasons.Add("MRZ-like xung đột với tín hiệu chân dung trung tâm và ảnh ít chữ, giảm thiên lệch mặt sau.");
        }

        if (portraitLikeDetected && mrzBandStrength < 0.2)
        {
            frontScore += 0.1;
        }

        if (leftSkinRatio > 0.27 && centerSkinRatio < 0.12 && midLeftInkDensity > 0.24)
        {
            frontScore -= 0.45;
            backScore += 0.16;
            reasons.Add("Vùng da lệch trái bất thường nhưng mật độ chữ vùng trái cao, khả năng dương tính giả chân dung trên mặt sau.");
        }

        if (frontScore > backScore && !portraitLikeDetected && !frontPhotoLayoutLike && mrzBandStrength >= 0.2)
        {
            frontScore -= 0.18;
            reasons.Add("Thiếu tín hiệu chân dung rõ trong khi dải text đáy hiện diện, giảm độ tin cậy mặt trước.");
        }

        var aspectRatio = image.Width / (double)image.Height;
        if (aspectRatio is > 1.45 and < 1.8)
        {
            frontScore += 0.05;
            backScore += 0.05;
            reasons.Add("Tỷ lệ ảnh gần với kích thước thẻ CCCD.");
        }

        var side = CitizenIdDetectedSides.Unknown;
        var confidence = 0.0;

        var maxScore = Math.Max(frontScore, backScore);
        var scoreGap = Math.Abs(frontScore - backScore);
        var isCardAspect = aspectRatio is > 1.08 and < 2.15;
        var weakCardAspect = aspectRatio is > 0.95 and < 2.4;
        var verticalPhotoAspect = aspectRatio is > 0.5 and < 0.95;
        var hintSuggestsCitizenId = frontHintMatched || backHintMatched;

        var frontSignalCount = 0;
        if (portraitLikeDetected)
        {
            frontSignalCount++;
        }

        if (emblemLikeDetected)
        {
            frontSignalCount++;
        }

        if (frontPhotoLayoutLike)
        {
            frontSignalCount++;
        }

        if (centerSkinRatio >= 0.12)
        {
            frontSignalCount++;
        }

        var backSignalCount = 0;
        if (qrReliable)
        {
            backSignalCount++;
        }

        if (mrzReliable)
        {
            backSignalCount++;
        }

        if (backRegionInkDensity >= 0.3)
        {
            backSignalCount++;
        }

        if (textHeavyBothSides)
        {
            backSignalCount++;
        }

        if (asymmetricTextLayout)
        {
            backSignalCount++;
        }

        if (structuralBackLayoutLike)
        {
            backSignalCount++;
        }

        var strongFrontSignalCount = 0;
        if (portraitLikeDetected)
        {
            strongFrontSignalCount++;
        }

        if (emblemLikeDetected)
        {
            strongFrontSignalCount++;
        }

        if (frontPhotoLayoutLike)
        {
            strongFrontSignalCount++;
        }

        var strongBackSignalCount = 0;
        if (qrReliable)
        {
            strongBackSignalCount++;
        }

        if (mrzReliable && mrzBandStrength >= 0.32 && topBandInkDensity >= 0.06)
        {
            strongBackSignalCount++;
        }

        if (structuralBackLayoutLike)
        {
            strongBackSignalCount++;
        }

        if (textHeavyBothSides && topBandInkDensity >= 0.18 && (qrReliable || mrzReliable || backHintMatched))
        {
            strongBackSignalCount++;
        }

        var weakBackSignalCount = 0;
        if (backRegionInkDensity >= 0.3)
        {
            weakBackSignalCount++;
        }

        if (asymmetricTextLayout)
        {
            weakBackSignalCount++;
        }

        if (uniformTextDistribution && topBandInkDensity > 0.18)
        {
            weakBackSignalCount++;
        }

        var strongFrontEvidence = frontSignalCount >= 2
            && (frontPhotoLayoutLike || emblemLikeDetected || centerSkinRatio >= 0.2 || frontHintMatched);
        var strongBackEvidence = strongBackSignalCount >= 1
            && (backSignalCount >= 2 || backHintMatched);

        var likelyCitizenId = (isCardAspect
            && (
                strongFrontEvidence
                || strongBackEvidence
                || structuralBackLayoutLike
                || (hintSuggestsCitizenId && (frontSignalCount + backSignalCount) >= 2)
                || (mrzReliable && backRegionInkDensity >= 0.22)
            ))
            || (weakCardAspect && hintSuggestsCitizenId && backSignalCount >= 2)
            || (weakCardAspect && hintSuggestsCitizenId && frontSignalCount >= 1 && centerSkinRatio >= 0.08)
            || (verticalPhotoAspect && (strongFrontEvidence || strongBackEvidence));

        if (!likelyCitizenId)
        {
            side = CitizenIdDetectedSides.Unknown;
            confidence = 0.3;
            reasons.Add("Ảnh chưa có đủ tín hiệu cấu trúc CCCD; hệ thống trả unknown để tránh nhận diện sai.");
        }
        else if (!hintSuggestsCitizenId && strongFrontSignalCount == 0 && strongBackSignalCount == 0)
        {
            side = CitizenIdDetectedSides.Unknown;
            confidence = 0.3;
            reasons.Add("Không có tín hiệu mạnh đặc trưng CCCD (front/back), trả unknown để tránh mặc định nhầm sang back.");
        }
        else
        {
            if (maxScore >= 0.3)
            {
                if (scoreGap < 0.12 && maxScore < 0.62)
                {
                    side = CitizenIdDetectedSides.Unknown;
                    confidence = 0.45;
                    reasons.Add("Tín hiệu front/back quá sát nhau, chưa đủ chắc chắn để kết luận.");
                }
                else
                {
                    side = frontScore >= backScore ? CitizenIdDetectedSides.Front : CitizenIdDetectedSides.Back;

                    if (side == CitizenIdDetectedSides.Back)
                    {
                        var hasReliableBackEvidence = strongBackSignalCount >= 1
                            || (backHintMatched && weakBackSignalCount >= 2 && backSignalCount >= 2);

                        if (!hasReliableBackEvidence)
                        {
                            side = CitizenIdDetectedSides.Unknown;
                            confidence = 0.4;
                            reasons.Add("Thiếu tín hiệu back mạnh (QR/MRZ/text-band), trả unknown để tránh false positive trên ảnh không phải CCCD.");
                        }
                    }

                    if (side == CitizenIdDetectedSides.Front && strongFrontSignalCount == 0)
                    {
                        side = CitizenIdDetectedSides.Unknown;
                        confidence = 0.4;
                        reasons.Add("Thiếu tín hiệu mặt trước mạnh, trả unknown để tránh false positive.");
                    }

                    if (side != CitizenIdDetectedSides.Unknown)
                    {
                        confidence = Math.Clamp(0.52 + scoreGap * 0.45 + maxScore * 0.18, 0.52, 0.985);
                    }
                }
            }
            else
            {
                reasons.Add("Không đủ tín hiệu chắc chắn để phân loại front/back.");
                confidence = 0.35;
            }
        }

        return new CitizenIdSideDetectResponse
        {
            Side = side,
            Confidence = Math.Round(confidence, 4, MidpointRounding.AwayFromZero),
            Reasons = reasons,
            Signals = new CitizenIdSideDetectSignals
            {
                QrDetected = qrDetected,
                BarcodeDetected = barcodeDetected,
                PortraitLikeDetected = portraitLikeDetected,
                EmblemLikeDetected = emblemLikeDetected,
                FrontPhotoLayoutLike = frontPhotoLayoutLike,
                TextHeavyBothSides = textHeavyBothSides,
                UniformTextDistribution = uniformTextDistribution,
                StructuralBackLayoutLike = structuralBackLayoutLike,
                QrReliable = qrReliable,
                MrzReliable = mrzReliable,
                LikelyCitizenId = likelyCitizenId,
                FrontSignalCount = frontSignalCount,
                BackSignalCount = backSignalCount,
                StrongFrontSignalCount = strongFrontSignalCount,
                StrongBackSignalCount = strongBackSignalCount,
                CenterSkinRatio = Math.Round(centerSkinRatio, 4, MidpointRounding.AwayFromZero),
                LeftSkinRatio = Math.Round(leftSkinRatio, 4, MidpointRounding.AwayFromZero),
                RightSkinRatio = Math.Round(rightSkinRatio, 4, MidpointRounding.AwayFromZero),
                MrzBandStrength = Math.Round(mrzBandStrength, 4, MidpointRounding.AwayFromZero),
                BackRegionInkDensity = Math.Round(backRegionInkDensity, 4, MidpointRounding.AwayFromZero),
                MidLeftInkDensity = Math.Round(midLeftInkDensity, 4, MidpointRounding.AwayFromZero),
                MidRightInkDensity = Math.Round(midRightInkDensity, 4, MidpointRounding.AwayFromZero),
                TopBandInkDensity = Math.Round(topBandInkDensity, 4, MidpointRounding.AwayFromZero),
                Width = image.Width,
                Height = image.Height
            }
        };
    }

    private static Image<Rgba32> CreateWorkingImage(Image<Rgba32> sourceImage, ICollection<string> reasons)
    {
        var cardRegion = DetectLikelyCardRegion(sourceImage);
        if (cardRegion is null)
        {
            return sourceImage.Clone();
        }

        reasons.Add("Đã tự động cắt vùng thẻ để giảm nhiễu nền khi phân tích.");
        return sourceImage.Clone(context => context.Crop(cardRegion.Value));
    }

    private static Rectangle? DetectLikelyCardRegion(Image<Rgba32> image)
    {
        var stepX = Math.Max(1, image.Width / 480);
        var stepY = Math.Max(1, image.Height / 480);

        var minX = image.Width;
        var minY = image.Height;
        var maxX = -1;
        var maxY = -1;
        long candidateCount = 0;

        for (var y = 0; y < image.Height; y += stepY)
        {
            for (var x = 0; x < image.Width; x += stepX)
            {
                if (!IsLikelyCardPixel(image[x, y]))
                {
                    continue;
                }

                candidateCount++;
                if (x < minX)
                {
                    minX = x;
                }

                if (y < minY)
                {
                    minY = y;
                }

                if (x > maxX)
                {
                    maxX = x;
                }

                if (y > maxY)
                {
                    maxY = y;
                }
            }
        }

        if (candidateCount < 500 || maxX <= minX || maxY <= minY)
        {
            return null;
        }

        var padX = Math.Max(6, (maxX - minX) / 12);
        var padY = Math.Max(6, (maxY - minY) / 12);

        minX = Math.Max(0, minX - padX);
        minY = Math.Max(0, minY - padY);
        maxX = Math.Min(image.Width - 1, maxX + padX);
        maxY = Math.Min(image.Height - 1, maxY + padY);

        var width = maxX - minX + 1;
        var height = maxY - minY + 1;
        var areaRatio = (width * (double)height) / (image.Width * (double)image.Height);
        var aspectRatio = width / (double)height;

        // Reject boxes that are too small/large or far from ID-card layout.
        if (areaRatio is < 0.04 or > 0.9 || aspectRatio is < 1.15 or > 2.4)
        {
            return null;
        }

        return new Rectangle(minX, minY, width, height);
    }

    private static bool IsLikelyCardPixel(Rgba32 pixel)
    {
        var max = Math.Max(pixel.R, Math.Max(pixel.G, pixel.B));
        var min = Math.Min(pixel.R, Math.Min(pixel.G, pixel.B));

        if (max == 0)
        {
            return false;
        }

        // Saturation-based filter helps ignore white/gray backgrounds.
        var saturation = (max - min) / (double)max;
        return saturation >= 0.12;
    }

    private static bool DetectQrLikePattern(Image<Rgba32> image, double adaptiveInkThreshold)
    {
        var startX = image.Width * 55 / 100;
        var startY = image.Height * 45 / 100;
        var endX = image.Width - 1;
        var endY = image.Height - 1;

        if (startX >= endX || startY >= endY)
        {
            return false;
        }

        long darkCount = 0;
        long lightCount = 0;
        long horizontalTransitions = 0;
        long verticalTransitions = 0;

        for (var y = startY; y <= endY; y += 2)
        {
            var previousIsDark = false;
            var hasPrevious = false;
            for (var x = startX; x <= endX; x += 2)
            {
                var pixel = image[x, y];
                var luminance = (pixel.R * 0.2126) + (pixel.G * 0.7152) + (pixel.B * 0.0722);
                var isDark = luminance < adaptiveInkThreshold;

                if (isDark)
                {
                    darkCount++;
                }
                else if (luminance > 165)
                {
                    lightCount++;
                }

                if (hasPrevious && previousIsDark != isDark)
                {
                    horizontalTransitions++;
                }

                previousIsDark = isDark;
                hasPrevious = true;
            }
        }

        for (var x = startX; x <= endX; x += 2)
        {
            var previousIsDark = false;
            var hasPrevious = false;
            for (var y = startY; y <= endY; y += 2)
            {
                var pixel = image[x, y];
                var luminance = (pixel.R * 0.2126) + (pixel.G * 0.7152) + (pixel.B * 0.0722);
                var isDark = luminance < adaptiveInkThreshold;

                if (hasPrevious && previousIsDark != isDark)
                {
                    verticalTransitions++;
                }

                previousIsDark = isDark;
                hasPrevious = true;
            }
        }

        var samples = darkCount + lightCount;
        if (samples == 0)
        {
            return false;
        }

        var darkRatio = darkCount / (double)samples;
        var horizontalTransitionRatio = horizontalTransitions / Math.Max(1.0, samples);
        var verticalTransitionRatio = verticalTransitions / Math.Max(1.0, samples);
        var transitionBalance = Math.Abs(horizontalTransitionRatio - verticalTransitionRatio);

        // QR-like area tends to have balanced dark/light blocks with frequent transitions in both axes.
        return darkRatio is > 0.28 and < 0.64
            && horizontalTransitionRatio > 0.09
            && verticalTransitionRatio > 0.09
            && transitionBalance < 0.05;
    }

    private static bool DetectNationalEmblemLike(Image<Rgba32> image)
    {
        var startX = (int)(image.Width * 0.04);
        var endX = (int)(image.Width * 0.32);
        var startY = (int)(image.Height * 0.05);
        var endY = (int)(image.Height * 0.35);

        if (endX <= startX || endY <= startY)
        {
            return false;
        }

        long total = 0;
        long warm = 0;

        for (var y = startY; y < endY; y++)
        {
            for (var x = startX; x < endX; x++)
            {
                var pixel = image[x, y];
                total++;

                if (pixel.R > 120 && pixel.R > (pixel.G + 14) && pixel.G > (pixel.B + 6))
                {
                    warm++;
                }
            }
        }

        if (total == 0)
        {
            return false;
        }

        var warmRatio = warm / (double)total;
        return warmRatio >= 0.06;
    }

    private static double ComputeAdaptiveInkThreshold(Image<Rgba32> image)
    {
        long sampleCount = 0;
        double luminanceSum = 0;

        for (var y = 0; y < image.Height; y += 4)
        {
            for (var x = 0; x < image.Width; x += 4)
            {
                var pixel = image[x, y];
                luminanceSum += (pixel.R * 0.2126) + (pixel.G * 0.7152) + (pixel.B * 0.0722);
                sampleCount++;
            }
        }

        if (sampleCount == 0)
        {
            return 115;
        }

        var avg = luminanceSum / sampleCount;
        return Math.Clamp(avg * 0.78, 75, 150);
    }

    private static double ComputeSkinRatio(Image<Rgba32> image, double startXRatio, double endXRatio, double startYRatio, double endYRatio)
    {
        var startX = (int)(image.Width * startXRatio);
        var endX = (int)(image.Width * endXRatio);
        var startY = (int)(image.Height * startYRatio);
        var endY = (int)(image.Height * endYRatio);

        if (endX <= startX || endY <= startY)
        {
            return 0;
        }

        long total = 0;
        long skin = 0;

        for (var y = startY; y < endY; y++)
        {
            for (var x = startX; x < endX; x++)
            {
            var pixel = image[x, y];
                var r = pixel.R;
                var g = pixel.G;
                var b = pixel.B;

                // YCbCr-based skin cluster heuristic.
                var cb = 128 - 0.168736 * r - 0.331264 * g + 0.5 * b;
                var cr = 128 + 0.5 * r - 0.418688 * g - 0.081312 * b;

                total++;
                if (cb is >= 77 and <= 127 && cr is >= 133 and <= 173)
                {
                    skin++;
                }
            }
        }

        return total == 0 ? 0 : skin / (double)total;
    }

    private static double ComputeInkDensity(Image<Rgba32> image, double startXRatio, double endXRatio, double startYRatio, double endYRatio, double inkThreshold)
    {
        var startX = (int)(image.Width * startXRatio);
        var endX = (int)(image.Width * endXRatio);
        var startY = (int)(image.Height * startYRatio);
        var endY = (int)(image.Height * endYRatio);

        if (endX <= startX || endY <= startY)
        {
            return 0;
        }

        long total = 0;
        long dark = 0;

        for (var y = startY; y < endY; y++)
        {
            for (var x = startX; x < endX; x++)
            {
                var pixel = image[x, y];
                var luminance = (pixel.R * 0.2126) + (pixel.G * 0.7152) + (pixel.B * 0.0722);
                total++;
                if (luminance < inkThreshold)
                {
                    dark++;
                }
            }
        }

        return total == 0 ? 0 : dark / (double)total;
    }

    private static double EstimateMrzBandStrength(Image<Rgba32> image, double inkThreshold)
    {
        var startX = (int)(image.Width * 0.08);
        var endX = (int)(image.Width * 0.92);
        var startY = (int)(image.Height * 0.70);
        var endY = (int)(image.Height * 0.96);

        if (endX <= startX || endY <= startY)
        {
            return 0;
        }

        long strongRows = 0;
        long totalRows = 0;

        for (var y = startY; y < endY; y++)
        {
            long dark = 0;
            long total = 0;
            long transitions = 0;
            var previousIsDark = false;
            var hasPrevious = false;

            for (var x = startX; x < endX; x++)
            {
                var pixel = image[x, y];
                var luminance = (pixel.R * 0.2126) + (pixel.G * 0.7152) + (pixel.B * 0.0722);
                var isDark = luminance < inkThreshold;
                total++;
                if (isDark)
                {
                    dark++;
                }

                if (hasPrevious && previousIsDark != isDark)
                {
                    transitions++;
                }

                previousIsDark = isDark;
                hasPrevious = true;
            }

            if (total == 0)
            {
                continue;
            }

            var darkRatio = dark / (double)total;
            var transitionRatio = transitions / (double)total;
            totalRows++;

            // Text-like lines should have balanced ink and frequent edge transitions.
            if (darkRatio is > 0.14 and < 0.72 && transitionRatio > 0.06)
            {
                strongRows++;
            }
        }

        if (totalRows == 0)
        {
            return 0;
        }

        return strongRows / (double)totalRows;
    }
}
