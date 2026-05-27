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
    private readonly ActivityLogIngestQueueService _activityLogIngestQueueService;
    private readonly LogActionDefinitionService _logActionDefinitionService;

    public PartnerController(
        PartnerService partnerService,
        ActivityLogService activityLogService,
        ActivityLogIngestQueueService activityLogIngestQueueService,
        LogActionDefinitionService logActionDefinitionService)
    {
        _partnerService = partnerService;
        _activityLogService = activityLogService;
        _activityLogIngestQueueService = activityLogIngestQueueService;
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

        var enqueueResult = await _activityLogIngestQueueService.EnqueueAsync(partner, request, HttpContext, cancellationToken);

        SetPartnerContext(partner);
        return Accepted(new
        {
            message = enqueueResult.Accepted
                ? "Đã tiếp nhận yêu cầu ghi log. Hệ thống sẽ xử lý bất đồng bộ trong hàng đợi."
                : "Yêu cầu ghi log với requestId này đã được tiếp nhận trước đó.",
            requestId = enqueueResult.RequestId
        });
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
            Action = string.IsNullOrWhiteSpace(filter.Action) ? null : filter.Action.Trim().ToUpperInvariant(),
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
            Action = string.IsNullOrWhiteSpace(filter.Action) ? null : filter.Action.Trim().ToUpperInvariant(),
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
}