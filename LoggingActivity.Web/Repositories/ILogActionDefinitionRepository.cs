using LoggingActivity.Web.Models;

namespace LoggingActivity.Web.Repositories;

public interface ILogActionDefinitionRepository
{
    Task<LogActionDefinition?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LogActionDefinition>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LogActionDefinition>> GetActiveAsync(CancellationToken cancellationToken = default);

    Task<PagedResult<LogActionDefinition>> GetPagedAsync(LogActionQuery query, CancellationToken cancellationToken = default);

    Task UpsertAsync(LogActionDefinition definition, string? existingCode = null, CancellationToken cancellationToken = default);

    Task DeleteAsync(string code, CancellationToken cancellationToken = default);
}