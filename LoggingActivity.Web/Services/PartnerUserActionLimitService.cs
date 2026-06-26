using LoggingActivity.Web.Infrastructure;
using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;

namespace LoggingActivity.Web.Services;

public sealed class PartnerUserActionLimitService
{
    private readonly IPartnerUserActionLimitRuleRepository _repository;
    private readonly IActivityLogRepository _activityLogRepository;

    public PartnerUserActionLimitService(
        IPartnerUserActionLimitRuleRepository repository,
        IActivityLogRepository activityLogRepository)
    {
        _repository = repository;
        _activityLogRepository = activityLogRepository;
    }

    public async Task<PartnerUserActionQuotaCheckResult> CheckAsync(
        string partnerId,
        string userId,
        string? userKeyType,
        string action,
        CancellationToken cancellationToken = default)
    {
        var normalizedPartnerId = string.IsNullOrWhiteSpace(partnerId) ? string.Empty : partnerId.Trim();
        var actorIdentifier = ActorIdentityHelper.NormalizeIdentifier(userId);
        var actorIdentifierType = ActorIdentityHelper.NormalizeType(userKeyType, actorIdentifier);
        var normalizedAction = string.IsNullOrWhiteSpace(action) ? string.Empty : action.Trim().ToUpperInvariant();

        if (string.IsNullOrWhiteSpace(normalizedPartnerId)
            || string.IsNullOrWhiteSpace(actorIdentifier)
            || string.IsNullOrWhiteSpace(normalizedAction))
        {
            return new PartnerUserActionQuotaCheckResult
            {
                PartnerId = normalizedPartnerId,
                ActorIdentifier = actorIdentifier,
                ActorIdentifierType = actorIdentifierType,
                Action = normalizedAction,
                HasLimit = false,
                IsAllowed = true,
                Message = "Thiếu thông tin để kiểm tra hạn mức."
            };
        }

        var rule = await _repository.GetByScopeAsync(normalizedPartnerId, actorIdentifier, normalizedAction, cancellationToken);
        if (rule is null || !rule.IsActive)
        {
            return new PartnerUserActionQuotaCheckResult
            {
                PartnerId = normalizedPartnerId,
                ActorIdentifier = actorIdentifier,
                ActorIdentifierType = actorIdentifierType,
                Action = normalizedAction,
                HasLimit = false,
                IsAllowed = true,
                Message = "User chưa được cấu hình hạn mức cho action này."
            };
        }

        var localToday = VietnamTimeExtensions.TodayInVietnamDate();
        var fromUtc = VietnamTimeExtensions.VietnamDateToUtcStart(localToday);
        var toUtc = VietnamTimeExtensions.VietnamDateToUtcStart(localToday.AddDays(1));

        var usedCount = await _activityLogRepository.GetPartnerUserActionCountAsync(
            normalizedPartnerId,
            actorIdentifier,
            actorIdentifierType,
            normalizedAction,
            fromUtc,
            toUtc,
            cancellationToken);

        var remaining = Math.Max(0, rule.DailyLimit - usedCount);
        var allowed = usedCount < rule.DailyLimit;

        return new PartnerUserActionQuotaCheckResult
        {
            PartnerId = normalizedPartnerId,
            ActorIdentifier = actorIdentifier,
            ActorIdentifierType = actorIdentifierType,
            Action = normalizedAction,
            HasLimit = true,
            DailyLimit = rule.DailyLimit,
            UsedCount = usedCount,
            RemainingCount = remaining,
            IsAllowed = allowed,
            Message = allowed
                ? $"User còn {remaining} lượt cho action '{normalizedAction}' trong hôm nay."
                : $"User đã chạm ngưỡng {rule.DailyLimit} lượt/ngày cho action '{normalizedAction}'."
        };
    }

    public Task<IReadOnlyList<PartnerUserActionLimitRule>> GetByPartnerAsync(string partnerId, CancellationToken cancellationToken = default)
    {
        return _repository.GetByPartnerAsync(partnerId, cancellationToken);
    }

    public Task<IReadOnlyList<PartnerUserActionLimitRule>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public async Task UpsertAsync(
        string partnerId,
        string userId,
        string? userKeyType,
        string action,
        int dailyLimit,
        bool isActive,
        CancellationToken cancellationToken = default)
    {
        var normalizedPartnerId = partnerId.Trim();
        var actorIdentifier = ActorIdentityHelper.NormalizeIdentifier(userId);
        var actorIdentifierType = ActorIdentityHelper.NormalizeType(userKeyType, actorIdentifier);
        var normalizedAction = action.Trim().ToUpperInvariant();

        var existing = await _repository.GetByScopeAsync(normalizedPartnerId, actorIdentifier, normalizedAction, cancellationToken);
        await _repository.UpsertAsync(new PartnerUserActionLimitRule
        {
            Id = existing?.Id,
            PartnerId = normalizedPartnerId,
            ActorIdentifier = actorIdentifier,
            ActorIdentifierType = actorIdentifierType,
            Action = normalizedAction,
            DailyLimit = dailyLimit,
            IsActive = isActive,
            CreatedAtUtc = existing?.CreatedAtUtc ?? DateTime.UtcNow
        }, cancellationToken);
    }

    public Task DeleteAsync(string partnerId, string userId, string action, CancellationToken cancellationToken = default)
    {
        var normalizedPartnerId = partnerId.Trim();
        var actorIdentifier = ActorIdentityHelper.NormalizeIdentifier(userId);
        var normalizedAction = action.Trim().ToUpperInvariant();
        return _repository.DeleteAsync(normalizedPartnerId, actorIdentifier, normalizedAction, cancellationToken);
    }
}
