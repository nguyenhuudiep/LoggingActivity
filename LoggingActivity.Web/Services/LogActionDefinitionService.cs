using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;

namespace LoggingActivity.Web.Services;

public sealed class LogActionDefinitionService
{
    private readonly ILogActionDefinitionRepository _repository;

    public LogActionDefinitionService(ILogActionDefinitionRepository repository)
    {
        _repository = repository;
    }

    public Task<IReadOnlyList<LogActionDefinition>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetAllAsync(cancellationToken);
    }

    public Task<LogActionDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return _repository.GetByCodeAsync(code, cancellationToken);
    }

    public Task<IReadOnlyList<LogActionDefinition>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        return _repository.GetActiveAsync(cancellationToken);
    }

    public Task<PagedResult<LogActionDefinition>> GetPagedAsync(LogActionQuery query, CancellationToken cancellationToken = default)
    {
        return _repository.GetPagedAsync(query, cancellationToken);
    }

    public async Task<bool> IsActiveActionConfiguredAsync(string actionCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(actionCode))
        {
            return false;
        }

        var normalizedCode = actionCode.Trim();
        var activeActions = await _repository.GetActiveAsync(cancellationToken);
        return activeActions.Any(item => string.Equals(item.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
    }

    public async Task<(bool Success, string? NormalizedCode, string? Error)> EnsureApiActionReadyAsync(string actionCode, CancellationToken cancellationToken = default)
    {
        var normalizedCode = NormalizeActionCode(actionCode);
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return (false, null, "Mã action không được để trống.");
        }

        var allActions = await _repository.GetAllAsync(cancellationToken);
        var existing = allActions.FirstOrDefault(item => string.Equals(item.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));

        if (existing is null)
        {
            await _repository.UpsertAsync(new LogActionDefinition
            {
                Code = normalizedCode,
                DisplayName = normalizedCode,
                Description = string.Empty,
                IsActive = true,
                CreatedAtUtc = DateTime.UtcNow
            }, cancellationToken: cancellationToken);

            return (true, normalizedCode, null);
        }

        if (!string.Equals(existing.Code, normalizedCode, StringComparison.Ordinal))
        {
            await _repository.UpsertAsync(new LogActionDefinition
            {
                Id = existing.Id,
                Code = normalizedCode,
                DisplayName = existing.DisplayName,
                Description = existing.Description,
                IsActive = existing.IsActive,
                CreatedAtUtc = existing.CreatedAtUtc
            }, existing.Code, cancellationToken);
        }

        if (!existing.IsActive)
        {
            return (false, normalizedCode, $"Action '{normalizedCode}' đang bị tắt. Hãy bật lại trong menu Action log trước khi gửi log.");
        }

        return (true, normalizedCode, null);
    }

    public async Task<(bool Success, string? Error)> UpsertAsync(string? existingCode, string code, string displayName, string description, bool isActive, CancellationToken cancellationToken = default)
    {
        var normalizedCode = code.Trim();
        if (string.IsNullOrWhiteSpace(normalizedCode))
        {
            return (false, "Mã action không được để trống.");
        }

        var allActions = await _repository.GetAllAsync(cancellationToken);
        var existing = allActions
            .FirstOrDefault(item => string.Equals(item.Code, normalizedCode, StringComparison.OrdinalIgnoreCase));
        var editingAction = !string.IsNullOrWhiteSpace(existingCode)
            ? allActions.FirstOrDefault(item => string.Equals(item.Code, existingCode, StringComparison.OrdinalIgnoreCase))
            : null;

        if (editingAction is null && !string.IsNullOrWhiteSpace(existingCode))
        {
            return (false, "Không tìm thấy action cần cập nhật.");
        }

        if (existing is not null && existing.Id != editingAction?.Id)
        {
            return (false, $"Mã action '{normalizedCode}' đã tồn tại.");
        }

        await _repository.UpsertAsync(new LogActionDefinition
        {
            Id = editingAction?.Id,
            Code = normalizedCode,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? normalizedCode : displayName.Trim(),
            Description = description?.Trim() ?? string.Empty,
            IsActive = editingAction?.IsActive ?? existing?.IsActive ?? isActive,
            CreatedAtUtc = editingAction?.CreatedAtUtc ?? existing?.CreatedAtUtc ?? DateTime.UtcNow
        }, editingAction?.Code, cancellationToken);

        return (true, null);
    }

    public async Task<bool> ToggleStatusAsync(string code, CancellationToken cancellationToken = default)
    {
        var existing = await _repository.GetByCodeAsync(code, cancellationToken);
        if (existing is null)
        {
            return false;
        }

        existing.IsActive = !existing.IsActive;
        await _repository.UpsertAsync(existing, cancellationToken: cancellationToken);
        return true;
    }

    public Task DeleteAsync(string id, CancellationToken cancellationToken = default)
    {
        return _repository.DeleteAsync(id, cancellationToken);
    }

    private static string NormalizeActionCode(string code)
    {
        return string.IsNullOrWhiteSpace(code)
            ? string.Empty
            : code.Trim().ToUpperInvariant();
    }
}