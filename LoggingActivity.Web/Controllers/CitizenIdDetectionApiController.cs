using LoggingActivity.Web.Infrastructure;
using LoggingActivity.Web.Contracts;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[ApiController]
[Authorize(Roles = SystemRoles.Admin + "," + SystemRoles.Auditor)]
[RequireFunctionPermission(AdminFunctionPermissions.CitizenIdDetection, AllowAuditor = true)]
[Route("api/admin/citizen-id")]
public sealed class CitizenIdDetectionApiController : ControllerBase
{
    private readonly CitizenIdDetectionService _detectionService;
    private readonly CitizenIdOpenAiService _citizenIdOpenAiService;
    private readonly ILogger<CitizenIdDetectionApiController> _logger;

    public CitizenIdDetectionApiController(
        CitizenIdDetectionService detectionService,
        CitizenIdOpenAiService citizenIdOpenAiService,
        ILogger<CitizenIdDetectionApiController> logger)
    {
        _detectionService = detectionService;
        _citizenIdOpenAiService = citizenIdOpenAiService;
        _logger = logger;
    }

    [HttpPost("detect-side")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<IActionResult> DetectSide([FromForm] CitizenIdSideDetectRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.Image is null || request.Image.Length == 0)
        {
            return BadRequest(new { message = "Thiếu file ảnh CCCD." });
        }

        if (request.Image.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { message = "Kích thước ảnh vượt quá 10MB." });
        }

        try
        {
            await using var stream = request.Image.OpenReadStream();
            var result = await _citizenIdOpenAiService.DetectSideAsync(stream, cancellationToken);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Khong the nhan dien mat truoc/sau CCCD.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể xử lý ảnh CCCD. Vui lòng thử lại với ảnh rõ hơn."
            });
        }
    }

    [HttpPost("extract-ocr")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<IActionResult> ExtractOcr([FromForm] CitizenIdSideDetectRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.Image is null || request.Image.Length == 0)
        {
            return BadRequest(new { message = "Thiếu file ảnh CCCD." });
        }

        if (request.Image.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { message = "Kích thước ảnh vượt quá 10MB." });
        }

        try
        {
            await using var stream = request.Image.OpenReadStream();
            var result = await _citizenIdOpenAiService.ExtractOcrAsync(stream, cancellationToken);
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
            _logger.LogWarning(ex, "Khong the OCR CCCD.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể OCR ảnh CCCD. Vui lòng thử lại với ảnh rõ hơn."
            });
        }
    }

    [HttpPost("detect-and-ocr")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    [RequestFormLimits(MultipartBodyLengthLimit = 10 * 1024 * 1024)]
    public async Task<IActionResult> DetectAndOcr([FromForm] CitizenIdSideDetectRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        if (request.Image is null || request.Image.Length == 0)
        {
            return BadRequest(new { message = "Thiếu file ảnh CCCD." });
        }

        if (request.Image.Length > 10 * 1024 * 1024)
        {
            return BadRequest(new { message = "Kích thước ảnh vượt quá 10MB." });
        }

        try
        {
            await using var stream = request.Image.OpenReadStream();
            var combined = await _citizenIdOpenAiService.DetectAndOcrAsync(stream, cancellationToken);
            if (string.Equals(combined.Status, "invalid_document", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest(new
                {
                    status = combined.Status,
                    message = combined.Message,
                    detect = combined.Detect
                });
            }

            return Ok(combined);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Khong the nhan dien + OCR CCCD.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể xử lý nhận diện + OCR ảnh CCCD. Vui lòng thử lại với ảnh rõ hơn."
            });
        }
    }

    [HttpPost("debug/replay-signals")]
    [Consumes("application/json")]
    public IActionResult ReplaySignals([FromBody] CitizenIdSideDetectSignals signals)
    {
        if (signals.Width <= 0 || signals.Height <= 0)
        {
            return BadRequest(new { message = "Width/Height trong signals phải lớn hơn 0." });
        }

        try
        {
            var result = _detectionService.ReplayFromSignals(signals);
            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Khong the replay nhan dien CCCD tu signals.");
            return StatusCode(StatusCodes.Status500InternalServerError, new
            {
                message = "Không thể replay nhận diện từ signals. Vui lòng kiểm tra dữ liệu đầu vào."
            });
        }
    }
}
