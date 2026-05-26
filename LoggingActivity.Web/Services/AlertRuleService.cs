using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;

namespace LoggingActivity.Web.Services;

public sealed class AlertRuleService
{
    private readonly IAlertRuleRepository _alertRuleRepository;
    private readonly IActivityLogRepository _activityLogRepository;
    private readonly ILogActionDefinitionRepository _logActionDefinitionRepository;
    private readonly AlertHistoryService _alertHistoryService;

    public AlertRuleService(
        IAlertRuleRepository alertRuleRepository,
        IActivityLogRepository activityLogRepository,
        ILogActionDefinitionRepository logActionDefinitionRepository,
        AlertHistoryService alertHistoryService)
    {
        _alertRuleRepository = alertRuleRepository;
        _activityLogRepository = activityLogRepository;
        _logActionDefinitionRepository = logActionDefinitionRepository;
        _alertHistoryService = alertHistoryService;
    }

    public Task<IReadOnlyList<AlertRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _alertRuleRepository.GetAllAsync(cancellationToken);
    }

    public Task<AlertRule?> GetByActionAsync(string action, CancellationToken cancellationToken = default)
    {
        return _alertRuleRepository.GetByActionAsync(action, cancellationToken);
    }

    public Task<PagedResult<AlertRule>> GetPagedAsync(AlertRuleQuery query, CancellationToken cancellationToken = default)
    {
        return _alertRuleRepository.GetPagedAsync(query, cancellationToken);
    }

    public async Task<IReadOnlyList<AlertWarning>> GetActiveWarningsAsync(CancellationToken cancellationToken = default)
    {
        var activeRules = await _alertRuleRepository.GetActiveAsync(cancellationToken);
        if (activeRules.Count == 0)
        {
            return Array.Empty<AlertWarning>();
        }

        var today = DateTime.UtcNow.Date;
        var tomorrow = today.AddDays(1);
        var userActionCounts = await _activityLogRepository.GetUserActionCountsAsync(today, tomorrow, cancellationToken);

        var warnings = userActionCounts
            .Join(
                activeRules,
                count => count.Action,
                rule => rule.Action,
                (count, rule) => new
                {
                    Rule = rule,
                    Count = count
                },
                StringComparer.OrdinalIgnoreCase)
            .Where(item => item.Count.CurrentCount >= item.Rule.DailyLimit)
            .OrderByDescending(item => item.Count.CurrentCount - item.Rule.DailyLimit)
            .ThenBy(item => item.Count.UserId)
            .Select(item => new AlertWarning
            {
                PartnerId = item.Count.PartnerId,
                PartnerName = item.Count.PartnerName,
                UserId = item.Count.UserId,
                UserName = item.Count.UserName,
                Action = item.Rule.Action,
                DailyLimit = item.Rule.DailyLimit,
                CurrentCount = item.Count.CurrentCount
            })
            .ToList();

            await BackfillAlertHistoryAsync(warnings, cancellationToken);
            return warnings;
    }

    public async Task RecordTriggeredAlertAsync(ActivityLog logEntry, CancellationToken cancellationToken = default)
    {
        if (!logEntry.ExternalUserId.HasValue || string.IsNullOrWhiteSpace(logEntry.Action))
        {
            return;
        }

        var rule = await _alertRuleRepository.GetByActionAsync(logEntry.Action.Trim(), cancellationToken);
        if (rule is null || !rule.IsActive)
        {
            return;
        }

        var alertDateUtc = logEntry.CreatedAtUtc.Date;
        var nextDateUtc = alertDateUtc.AddDays(1);
        var currentCount = await _activityLogRepository.GetUserActionCountAsync(
            logEntry.ExternalUserId.Value,
            logEntry.Action.Trim(),
            alertDateUtc,
            nextDateUtc,
            cancellationToken);

        if (currentCount < rule.DailyLimit)
        {
            return;
        }

        var alreadyRecorded = await _alertHistoryService.ExistsAsync(
            alertDateUtc,
            logEntry.ExternalUserId.Value,
            logEntry.Action.Trim(),
            cancellationToken);

        if (alreadyRecorded)
        {
            return;
        }

        var normalizedUserName = string.IsNullOrWhiteSpace(logEntry.UserName) ? "Anonymous" : logEntry.UserName.Trim();
        var normalizedPartnerName = string.IsNullOrWhiteSpace(logEntry.PartnerName) ? "N/A" : logEntry.PartnerName.Trim();

        await _alertHistoryService.AddAsync(new AlertHistory
        {
            PartnerId = logEntry.PartnerId,
            PartnerName = normalizedPartnerName,
            UserId = logEntry.ExternalUserId.Value,
            UserName = normalizedUserName,
            Action = logEntry.Action.Trim(),
            DailyLimit = rule.DailyLimit,
            CurrentCount = currentCount,
            AlertDateUtc = alertDateUtc,
            OccurredAtUtc = logEntry.CreatedAtUtc,
            Message = BuildAlertMessage(normalizedUserName, normalizedPartnerName, logEntry.Action.Trim(), rule.DailyLimit, currentCount)
        }, cancellationToken);
    }

    private async Task BackfillAlertHistoryAsync(IReadOnlyList<AlertWarning> warnings, CancellationToken cancellationToken)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        var todayUtc = DateTime.UtcNow.Date;
        foreach (var warning in warnings.Where(item => item.UserId.HasValue))
        {
            var userId = warning.UserId!.Value;
            var alreadyRecorded = await _alertHistoryService.ExistsAsync(todayUtc, userId, warning.Action, cancellationToken);
            if (alreadyRecorded)
            {
                continue;
            }

            await _alertHistoryService.AddAsync(new AlertHistory
            {
                PartnerId = warning.PartnerId,
                PartnerName = warning.PartnerName,
                UserId = userId,
                UserName = warning.UserName,
                Action = warning.Action,
                DailyLimit = warning.DailyLimit,
                CurrentCount = warning.CurrentCount,
                AlertDateUtc = todayUtc,
                OccurredAtUtc = DateTime.UtcNow,
                Message = BuildAlertMessage(warning.UserName, warning.PartnerName, warning.Action, warning.DailyLimit, warning.CurrentCount)
            }, cancellationToken);
        }
    }

    private static string BuildAlertMessage(string userName, string partnerName, string action, int dailyLimit, long currentCount)
    {
        return $"User '{userName}' của partner '{partnerName}' đã chạm hoặc vượt ngưỡng {dailyLimit}/ngày cho action '{action}' với {currentCount} log.";
    }

    public async Task<(bool Success, string? Error)> UpsertAsync(string? existingAction, string action, int dailyLimit, bool isActive, CancellationToken cancellationToken = default)
    {
        var normalizedAction = action.Trim();
        if (string.IsNullOrWhiteSpace(normalizedAction))
        {
            return (false, "Action cảnh báo không được để trống.");
        }

        var allRules = await _alertRuleRepository.GetAllAsync(cancellationToken);
        var existingRule = allRules
            .FirstOrDefault(rule => string.Equals(rule.Action, normalizedAction, StringComparison.OrdinalIgnoreCase));
        var editingRule = !string.IsNullOrWhiteSpace(existingAction)
            ? allRules.FirstOrDefault(rule => string.Equals(rule.Action, existingAction, StringComparison.OrdinalIgnoreCase))
            : null;

        if (editingRule is null && !string.IsNullOrWhiteSpace(existingAction))
        {
            return (false, "Không tìm thấy cấu hình cảnh báo cần cập nhật.");
        }

        if (existingRule is not null && existingRule.Id != editingRule?.Id)
        {
            return (false, $"Action '{normalizedAction}' đã có cấu hình cảnh báo.");
        }

        await _alertRuleRepository.UpsertAsync(new AlertRule
        {
            Id = editingRule?.Id,
            Action = normalizedAction,
            DailyLimit = dailyLimit,
            IsActive = editingRule?.IsActive ?? existingRule?.IsActive ?? isActive,
            CreatedAtUtc = editingRule?.CreatedAtUtc ?? existingRule?.CreatedAtUtc ?? DateTime.UtcNow
        }, cancellationToken);

        if (editingRule is not null
            && !string.Equals(editingRule.Action, normalizedAction, StringComparison.OrdinalIgnoreCase)
            && string.IsNullOrWhiteSpace(editingRule.Id))
        {
            await _alertRuleRepository.DeleteAsync(editingRule.Action, cancellationToken);
        }

        return (true, null);
    }

    public Task DeleteAsync(string action, CancellationToken cancellationToken = default)
    {
        return _alertRuleRepository.DeleteAsync(action, cancellationToken);
    }

    public async Task<bool> ToggleStatusAsync(string action, CancellationToken cancellationToken = default)
    {
        var existing = await _alertRuleRepository.GetByActionAsync(action, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        existing.IsActive = !existing.IsActive;
        await _alertRuleRepository.UpsertAsync(existing, cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<UnconfiguredLogActionWarning>> GetUnconfiguredActionWarningsAsync(CancellationToken cancellationToken = default)
    {
        var configuredActions = await _logActionDefinitionRepository.GetAllAsync(cancellationToken);
        var configuredCodes = configuredActions
            .Select(item => item.Code)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var actionCounts = await _activityLogRepository.GetActionCountsAsync(cancellationToken);

        return actionCounts
            .Where(item => !configuredCodes.Contains(item.Key))
            .OrderByDescending(item => item.Value)
            .Select(item => new UnconfiguredLogActionWarning
            {
                Action = item.Key,
                Count = item.Value
            })
            .ToList();
    }
}