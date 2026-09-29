using LoggingActivity.Web.Infrastructure;
using LoggingActivity.Web.Contracts;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[ApiController]
[Authorize(Roles = SystemRoles.Admin + "," + SystemRoles.Auditor)]
[RequireFunctionPermission(AdminFunctionPermissions.VehicleRegistrationOcr, AllowAuditor = true)]
[Route("api/admin/vehicle-registration")]
public sealed class VehicleRegistrationOcrApiController : ControllerBase
{
    private readonly VehicleRegistrationOcrService _ocrService;
    private readonly ILogger<VehicleRegistrationOcrApiController> _logger;

    public VehicleRegistrationOcrApiController(
        VehicleRegistrationOcrService ocrService,
        ILogger<VehicleRegistrationOcrApiController> logger)
    {
        _ocrService = ocrService;
        _logger = logger;
    }

    [HttpPost("extract-ocr")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<IActionResult> ExtractOcr([FromForm] VehicleRegistrationOcrRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.Image is null || request.Image.Length == 0)
        {
            return BadRequest(new { message = "Thiếu file ảnh giấy đăng ký xe." });
        }

        if (request.Image.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { message = "Kích thước ảnh vượt quá 10MB." });
        }

        try
        {
            await using var stream = request.Image.OpenReadStream();
            var result = await _ocrService.ExtractAsync(stream, "openai", openAiApiKeyOverride: null, cancellationToken);
            if (string.Equals(result.Status, "invalid_document", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    status = result.Status,
                    message = result.Message
                });
            }

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Khong the OCR giay dang ky xe.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể OCR giấy đăng ký xe. Vui lòng thử lại với ảnh rõ hơn."
            });
        }
    }

    [HttpPost("extract-ocr-compare")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<IActionResult> ExtractOcrCompare([FromForm] VehicleRegistrationOcrRequest request, [FromQuery] string? providers, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.Image is null || request.Image.Length == 0)
        {
            return BadRequest(new { message = "Thiếu file ảnh giấy đăng ký xe." });
        }

        if (request.Image.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { message = "Kích thước ảnh vượt quá 10MB." });
        }

        var normalizedProviders = ParseProviders(providers);
        if (normalizedProviders.Count == 0)
        {
            return BadRequest(new { message = "Danh sách provider không hợp lệ. Dùng: openai,custom." });
        }

        try
        {
            await using var uploadStream = request.Image.OpenReadStream();
            using var buffer = new MemoryStream();
            await uploadStream.CopyToAsync(buffer, cancellationToken);
            var imageBytes = buffer.ToArray();

            var tasks = normalizedProviders
                .Select(async provider =>
                {
                    await using var stream = new MemoryStream(imageBytes, writable: false);
                    var result = await _ocrService.ExtractAsync(stream, provider, request.OpenAiApiKey, cancellationToken);
                    return new VehicleRegistrationOcrCompareItem
                    {
                        Provider = provider,
                        Result = result
                    };
                })
                .ToArray();

            var results = await Task.WhenAll(tasks);
            var successCount = results.Count(item => string.Equals(item.Result.Status, "success", StringComparison.OrdinalIgnoreCase));
            var appliedAny = results.Any(item => item.Result.Applied);
            var invalidDocument = results.FirstOrDefault(item => string.Equals(item.Result.Status, "invalid_document", StringComparison.OrdinalIgnoreCase));
            if (invalidDocument is not null)
            {
                return BadRequest(new
                {
                    status = "invalid_document",
                    message = invalidDocument.Result.Message,
                    provider = invalidDocument.Provider
                });
            }

            return Ok(new VehicleRegistrationOcrCompareResult
            {
                AppliedAny = appliedAny,
                SuccessCount = successCount,
                Results = results
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Khong the so sanh OCR giay dang ky xe.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể so sánh OCR giấy đăng ký xe. Vui lòng thử lại với ảnh rõ hơn."
            });
        }
    }

    private static IReadOnlyList<string> ParseProviders(string? providers)
    {
        var source = string.IsNullOrWhiteSpace(providers) ? "openai,custom" : providers;
        var result = source
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(provider => provider.ToLowerInvariant())
            .Where(provider => provider is "openai" or "custom")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return result;
    }
}