using LoggingActivity.Web.Contracts;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[ApiController]
[Authorize(Roles = SystemRoles.Admin + "," + SystemRoles.Auditor)]
[Route("api/admin/citizen-id")]
public sealed class CitizenIdDetectionApiController : ControllerBase
{
    private readonly CitizenIdDetectionService _detectionService;
    private readonly ILogger<CitizenIdDetectionApiController> _logger;

    public CitizenIdDetectionApiController(
        CitizenIdDetectionService detectionService,
        ILogger<CitizenIdDetectionApiController> logger)
    {
        _detectionService = detectionService;
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
            var result = await _detectionService.DetectSideAsync(stream, cancellationToken);

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
}
