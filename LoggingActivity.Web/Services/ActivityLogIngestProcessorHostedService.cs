namespace LoggingActivity.Web.Services;

public sealed class ActivityLogIngestProcessorHostedService : BackgroundService
{
    private static readonly TimeSpan IdleDelay = TimeSpan.FromSeconds(1);

    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<ActivityLogIngestProcessorHostedService> _logger;

    public ActivityLogIngestProcessorHostedService(
        IServiceProvider serviceProvider,
        ILogger<ActivityLogIngestProcessorHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var workerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";

        try
        {
            using var startupScope = _serviceProvider.CreateScope();
            var startupQueueService = startupScope.ServiceProvider.GetRequiredService<ActivityLogIngestQueueService>();
            await startupQueueService.EnsureInfrastructureAsync(stoppingToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Không thể khởi tạo hạ tầng hàng đợi ingest log.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var queueService = scope.ServiceProvider.GetRequiredService<ActivityLogIngestQueueService>();
                var processed = await queueService.ProcessNextAsync(workerId, stoppingToken);

                if (!processed)
                {
                    await Task.Delay(IdleDelay, stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker ingest log gặp lỗi ngoài dự kiến.");
                await Task.Delay(IdleDelay, stoppingToken);
            }
        }
    }
}