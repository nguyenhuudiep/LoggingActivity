using LoggingActivity.Web.Models;
using LoggingActivity.Web.Repositories;

namespace LoggingActivity.Web.Services;

public sealed class PartnerService
{
    private readonly IPartnerRepository _partnerRepository;

    public PartnerService(IPartnerRepository partnerRepository)
    {
        _partnerRepository = partnerRepository;
    }

    public Task<IReadOnlyList<Partner>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return _partnerRepository.GetAllAsync(cancellationToken);
    }

    public Task<Partner?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return _partnerRepository.GetByIdAsync(id, cancellationToken);
    }

    public Task<Partner?> GetByApiKeyAsync(string apiKey, CancellationToken cancellationToken = default)
    {
        return _partnerRepository.GetByApiKeyAsync(apiKey, cancellationToken);
    }

    public async Task<(bool Success, string? Error)> CreateAsync(Partner partner, CancellationToken cancellationToken = default)
    {
        partner.Name = partner.Name.Trim();
        if (string.IsNullOrWhiteSpace(partner.Name))
        {
            return (false, "Tên đối tác không được để trống.");
        }

        partner.ApiKey = GenerateApiKey();
        partner.CreatedAtUtc = DateTime.UtcNow;
        partner.UpdatedAtUtc = DateTime.UtcNow;

        await _partnerRepository.CreateAsync(partner, cancellationToken);
        return (true, null);
    }

    public async Task<(bool Success, string? Error)> UpdateAsync(Partner partner, CancellationToken cancellationToken = default)
    {
        var existing = await _partnerRepository.GetByIdAsync(partner.Id!, cancellationToken);
        if (existing is null)
        {
            return (false, "Không tìm thấy đối tác.");
        }

        existing.Name = partner.Name.Trim();
        if (string.IsNullOrWhiteSpace(existing.Name))
        {
            return (false, "Tên đối tác không được để trống.");
        }

        existing.IsActive = partner.IsActive;

        await _partnerRepository.UpdateAsync(existing, cancellationToken);
        return (true, null);
    }

    public async Task<string?> RegenerateApiKeyAsync(string id, CancellationToken cancellationToken = default)
    {
        var existing = await _partnerRepository.GetByIdAsync(id, cancellationToken);
        if (existing is null)
        {
            return null;
        }

        existing.ApiKey = GenerateApiKey();
        await _partnerRepository.UpdateAsync(existing, cancellationToken);
        return existing.ApiKey;
    }

    public string GenerateApiKey()
    {
        return Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("/", string.Empty)
            .Replace("+", string.Empty)
            .Replace("=", string.Empty);
    }
}