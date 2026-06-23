using LoggingActivity.Web.Repositories;

namespace LoggingActivity.Web.Services;

public sealed class ActivityLogInfrastructureHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ActivityLogInfrastructureHostedService> _logger;

    public ActivityLogInfrastructureHostedService(
        IServiceProvider serviceProvider,
        ILogger<ActivityLogInfrastructureHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var activityLogRepository = scope.ServiceProvider.GetRequiredService<IActivityLogRepository>();
            await activityLogRepository.EnsureIndexesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Khong the dam bao index cho ActivityLogs khi khoi dong.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
