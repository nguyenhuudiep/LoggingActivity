using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;
using LoggingActivity.Web.Infrastructure;

namespace LoggingActivity.Web.Services;

public sealed class AlertRuleService
{
    private readonly IAlertRuleRepository _alertRuleRepository;
    private readonly IActivityLogRepository _activityLogRepository;
    private readonly ILogActionDefinitionRepository _logActionDefinitionRepository;
    private readonly AlertHistoryService _alertHistoryService;
    private readonly ThresholdNotificationService _thresholdNotificationService;

    public AlertRuleService(
        IAlertRuleRepository alertRuleRepository,
        IActivityLogRepository activityLogRepository,
        ILogActionDefinitionRepository logActionDefinitionRepository,
        AlertHistoryService alertHistoryService,
        ThresholdNotificationService thresholdNotificationService)
    {
        _alertRuleRepository = alertRuleRepository;
        _activityLogRepository = activityLogRepository;
        _logActionDefinitionRepository = logActionDefinitionRepository;
        _alertHistoryService = alertHistoryService;
        _thresholdNotificationService = thresholdNotificationService;
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

        var localToday = VietnamTimeExtensions.TodayInVietnamDate();
        var fromUtc = VietnamTimeExtensions.VietnamDateToUtcStart(localToday);
        var toUtc = VietnamTimeExtensions.VietnamDateToUtcStart(localToday.AddDays(1));
        var userActionCounts = await _activityLogRepository.GetUserActionCountsAsync(fromUtc, toUtc, cancellationToken);

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
            .ThenBy(item => item.Count.ActorIdentifier)
            .Select(item => new AlertWarning
            {
                PartnerId = item.Count.PartnerId,
                PartnerName = item.Count.PartnerName,
                UserId = item.Count.UserId,
                ActorIdentifier = item.Count.ActorIdentifier,
                ActorIdentifierType = item.Count.ActorIdentifierType,
                UserName = item.Count.UserName,
                Action = item.Rule.Action,
                DailyLimit = item.Rule.DailyLimit,
                CurrentCount = item.Count.CurrentCount
            })
            .ToList();

        await BackfillAlertHistoryAsync(warnings, cancellationToken);
        return warnings;
    }

    public async Task EnsureAlertHistoryBackfillAsync(DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken = default)
    {
        var activeRules = await _alertRuleRepository.GetActiveAsync(cancellationToken);
        if (activeRules.Count == 0)
        {
            return;
        }

        var counts = await _activityLogRepository.GetDailyUserActionCountsAsync(fromUtc, toUtc, cancellationToken);
        if (counts.Count == 0)
        {
            return;
        }

        var rulesByAction = activeRules.ToDictionary(rule => rule.Action.Trim(), StringComparer.OrdinalIgnoreCase);

        foreach (var count in counts)
        {
            if (!rulesByAction.TryGetValue(count.Action, out var rule) || count.CurrentCount < rule.DailyLimit)
            {
                continue;
            }

            var alreadyRecorded = await _alertHistoryService.ExistsAsync(count.AlertDateUtc, count.ActorIdentifier, count.Action, cancellationToken);
            if (alreadyRecorded)
            {
                continue;
            }

            await _alertHistoryService.AddAsync(new AlertHistory
            {
                PartnerId = count.PartnerId,
                PartnerName = count.PartnerName,
                UserId = count.UserId,
                ActorIdentifier = count.ActorIdentifier,
                ActorIdentifierType = count.ActorIdentifierType,
                UserName = count.UserName,
                Action = rule.Action,
                DailyLimit = rule.DailyLimit,
                CurrentCount = count.CurrentCount,
                AlertDateUtc = count.AlertDateUtc,
                OccurredAtUtc = count.OccurredAtUtc,
                Message = BuildAlertMessage(count.ActorIdentifier, count.ActorIdentifierType, count.UserName, count.PartnerName, rule.Action, rule.DailyLimit, count.CurrentCount)
            }, cancellationToken);
        }
    }

    public async Task RecordTriggeredAlertAsync(ActivityLog logEntry, CancellationToken cancellationToken = default)
    {
        var actorIdentifier = logEntry.DisplayActorIdentifier;
        if (string.IsNullOrWhiteSpace(actorIdentifier) || string.IsNullOrWhiteSpace(logEntry.Action))
        {
            return;
        }

        var rule = await _alertRuleRepository.GetByActionAsync(logEntry.Action.Trim(), cancellationToken);
        if (rule is null || !rule.IsActive)
        {
            return;
        }

        var localAlertDate = logEntry.CreatedAtUtc.ToVietnamTime().Date;
        var alertDateUtc = VietnamTimeExtensions.VietnamDateToUtcStart(localAlertDate);
        var nextDateUtc = VietnamTimeExtensions.VietnamDateToUtcStart(localAlertDate.AddDays(1));
        var currentCount = await _activityLogRepository.GetUserActionCountAsync(
            actorIdentifier,
            logEntry.DisplayActorIdentifierType,
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
            actorIdentifier,
            logEntry.Action.Trim(),
            cancellationToken);

        if (alreadyRecorded)
        {
            return;
        }

        var normalizedUserName = string.IsNullOrWhiteSpace(logEntry.UserName) ? "Anonymous" : logEntry.UserName.Trim();
        var normalizedPartnerName = string.IsNullOrWhiteSpace(logEntry.PartnerName) ? "N/A" : logEntry.PartnerName.Trim();

        var alertMessage = BuildAlertMessage(actorIdentifier, logEntry.DisplayActorIdentifierType, normalizedUserName, normalizedPartnerName, logEntry.Action.Trim(), rule.DailyLimit, currentCount);

        await _alertHistoryService.AddAsync(new AlertHistory
        {
            PartnerId = logEntry.PartnerId,
            PartnerName = normalizedPartnerName,
            UserId = logEntry.ExternalUserId,
            ActorIdentifier = actorIdentifier,
            ActorIdentifierType = logEntry.DisplayActorIdentifierType,
            UserName = normalizedUserName,
            Action = logEntry.Action.Trim(),
            DailyLimit = rule.DailyLimit,
            CurrentCount = currentCount,
            AlertDateUtc = alertDateUtc,
            OccurredAtUtc = logEntry.CreatedAtUtc,
            Message = alertMessage
        }, cancellationToken);

        await _thresholdNotificationService.NotifyAsync(alertMessage, cancellationToken);
    }

    private async Task BackfillAlertHistoryAsync(IReadOnlyList<AlertWarning> warnings, CancellationToken cancellationToken)
    {
        if (warnings.Count == 0)
        {
            return;
        }

        var todayUtc = VietnamTimeExtensions.VietnamDateToUtcStart(VietnamTimeExtensions.TodayInVietnamDate());
        foreach (var warning in warnings.Where(item => !string.IsNullOrWhiteSpace(item.DisplayActorIdentifier)))
        {
            var actorIdentifier = warning.DisplayActorIdentifier;
            var alreadyRecorded = await _alertHistoryService.ExistsAsync(todayUtc, actorIdentifier, warning.Action, cancellationToken);
            if (alreadyRecorded)
            {
                continue;
            }

            await _alertHistoryService.AddAsync(new AlertHistory
            {
                PartnerId = warning.PartnerId,
                PartnerName = warning.PartnerName,
                UserId = warning.UserId,
                ActorIdentifier = actorIdentifier,
                ActorIdentifierType = warning.DisplayActorIdentifierType,
                UserName = warning.UserName,
                Action = warning.Action,
                DailyLimit = warning.DailyLimit,
                CurrentCount = warning.CurrentCount,
                AlertDateUtc = todayUtc,
                OccurredAtUtc = DateTime.UtcNow,
                Message = BuildAlertMessage(actorIdentifier, warning.DisplayActorIdentifierType, warning.UserName, warning.PartnerName, warning.Action, warning.DailyLimit, warning.CurrentCount)
            }, cancellationToken);
        }
    }

    private static string BuildAlertMessage(string actorIdentifier, string actorIdentifierType, string userName, string partnerName, string action, int dailyLimit, long currentCount)
    {
        var keyLabel = ActorIdentityHelper.BuildDisplayLabel(actorIdentifierType);
        return $"{keyLabel} '{actorIdentifier}' của partner '{partnerName}' đã chạm hoặc vượt ngưỡng {dailyLimit}/ngày cho action '{action}' với {currentCount} log. User hiển thị: '{userName}'.";
    }

    public async Task<(bool Success, string? Error)> UpsertAsync(string? existingAction, string action, int dailyLimit, bool isActive, CancellationToken cancellationToken = default)
    {
        var normalizedAction = action.Trim().ToUpperInvariant();
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