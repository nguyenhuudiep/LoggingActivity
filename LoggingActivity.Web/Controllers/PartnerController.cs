using LoggingActivity.Web.Contracts;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Services;
using LoggingActivity.Web.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LoggingActivity.Web.Controllers;

[ApiController]
[Route("api/partner")]
public sealed class PartnerController : ControllerBase
{
    private readonly PartnerService _partnerService;
    private readonly ActivityLogService _activityLogService;
    private readonly LogActionDefinitionService _logActionDefinitionService;

    public PartnerController(
        PartnerService partnerService,
        ActivityLogService activityLogService,
        LogActionDefinitionService logActionDefinitionService)
    {
        _partnerService = partnerService;
        _activityLogService = activityLogService;
        _logActionDefinitionService = logActionDefinitionService;
    }

    [HttpPost("activity")]
    public async Task<IActionResult> CreateActivity([FromBody] PartnerActivityRequest request, CancellationToken cancellationToken)
    {
        var partner = await ValidatePartnerAsync(cancellationToken);
        if (partner is null)
        {
            return Unauthorized(new { message = "API key không hợp lệ." });
        }

        if (!await _logActionDefinitionService.IsActiveActionConfiguredAsync(request.Action, cancellationToken))
        {
            return BadRequest(new
            {
                message = $"Action '{request.Action}' chưa được cấu hình hoặc đang bị tắt. Hãy cấu hình trong menu Action log trước khi gửi log."
            });
        }

        await _activityLogService.AddAsync(new ActivityLog
        {
            PartnerId = partner.Id,
            PartnerName = partner.Name,
            UserId = partner.Id,
            ExternalUserId = request.UserId,
            UserName = request.UserName.Trim(),
            Role = "Partner",
            Action = request.Action,
            Description = BuildIntegratedDescription(partner.Name, request.UserName, request.Action),
            Endpoint = string.IsNullOrWhiteSpace(request.Endpoint) ? HttpContext.Request.Path : request.Endpoint,
            Source = ActivityLogSources.IntegratedApi,
            HttpMethod = HttpContext.Request.Method,
            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
            CreatedAtUtc = DateTime.UtcNow
        }, cancellationToken);

        SetPartnerContext(partner);
        return Ok(new { message = "Đã ghi nhận activity log." });
    }

    [HttpGet("activity")]
    public async Task<IActionResult> GetActivities([FromQuery] LogFilterViewModel filter, CancellationToken cancellationToken)
    {
        var partner = await ValidatePartnerAsync(cancellationToken);
        if (partner is null)
        {
            return Unauthorized(new { message = "API key không hợp lệ." });
        }

        var query = new LogQuery
        {
            SearchTerm = filter.SearchTerm,
            PartnerId = partner.Id,
            Action = filter.Action,
            Source = string.IsNullOrWhiteSpace(filter.Source) ? ActivityLogSources.IntegratedApi : filter.Source,
            FromUtc = filter.From,
            ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        var result = await _activityLogService.GetPagedByUserAsync(partner.Id!, query, cancellationToken);
        SetPartnerContext(partner);
        return Ok(result);
    }

    [HttpGet("statistics")]
    public async Task<IActionResult> GetStatistics([FromQuery] LogFilterViewModel filter, CancellationToken cancellationToken)
    {
        var partner = await ValidatePartnerAsync(cancellationToken);
        if (partner is null)
        {
            return Unauthorized(new { message = "API key không hợp lệ." });
        }

        var query = new LogQuery
        {
            SearchTerm = filter.SearchTerm,
            PartnerId = partner.Id,
            Action = filter.Action,
            Source = string.IsNullOrWhiteSpace(filter.Source) ? ActivityLogSources.IntegratedApi : filter.Source,
            FromUtc = filter.From,
            ToUtc = filter.To?.Date.AddDays(1).AddTicks(-1),
            Page = filter.Page,
            PageSize = filter.PageSize
        };

        var result = await _activityLogService.GetStatisticsByUserAsync(partner.Id!, query, cancellationToken);
        SetPartnerContext(partner);
        return Ok(result);
    }

    private async Task<Partner?> ValidatePartnerAsync(CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("X-Api-Key", out var apiKey) || string.IsNullOrWhiteSpace(apiKey))
        {
            return null;
        }

        return await _partnerService.GetByApiKeyAsync(apiKey.ToString(), cancellationToken);
    }

    private void SetPartnerContext(Partner partner)
    {
        HttpContext.Items["PartnerUserId"] = partner.Id;
        HttpContext.Items["PartnerUserName"] = partner.Name;
    }

    private static string BuildIntegratedDescription(string? partnerName, string? userName, string? action)
    {
        var normalizedPartnerName = string.IsNullOrWhiteSpace(partnerName) ? "N/A" : partnerName.Trim();
        var normalizedUserName = string.IsNullOrWhiteSpace(userName) ? "Anonymous" : userName.Trim();
        var normalizedAction = string.IsNullOrWhiteSpace(action) ? "N/A" : action.Trim();

        return $"Partner {normalizedPartnerName}, username {normalizedUserName} thực hiện thao tác {normalizedAction}.";
    }
}