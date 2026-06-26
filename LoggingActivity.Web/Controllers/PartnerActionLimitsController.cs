using LoggingActivity.Web.Contracts;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[ApiController]
[Authorize(Roles = SystemRoles.Admin)]
[Route("api/admin/partner-action-limits")]
public sealed class PartnerActionLimitsController : ControllerBase
{
    private readonly PartnerUserActionLimitService _service;

    public PartnerActionLimitsController(PartnerUserActionLimitService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetByPartner([FromQuery] string partnerId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(partnerId))
        {
            return BadRequest(new { message = "Thiếu partnerId." });
        }

        var result = await _service.GetByPartnerAsync(partnerId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Upsert([FromBody] PartnerUserActionLimitUpsertRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await _service.UpsertAsync(
            request.PartnerId,
            request.UserId,
            request.UserKeyType,
            request.Action,
            request.DailyLimit,
            request.IsActive,
            cancellationToken);

        return Ok(new { message = "Đã lưu cấu hình hạn mức user-action." });
    }

    [HttpDelete]
    public async Task<IActionResult> Delete([FromBody] PartnerUserActionLimitDeleteRequest request, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return ValidationProblem(ModelState);
        }

        await _service.DeleteAsync(request.PartnerId, request.UserId, request.Action, cancellationToken);
        return Ok(new { message = "Đã xóa cấu hình hạn mức user-action." });
    }
}
