using LoggingActivity.Web.Models;
using LoggingActivity.Web.Options;
using Microsoft.Extensions.Options;

namespace LoggingActivity.Web.Services;

public sealed class SeedAdminHostedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly SeedAdminOptions _options;
    private readonly ILogger<SeedAdminHostedService> _logger;

    public SeedAdminHostedService(
        IServiceProvider serviceProvider,
        IOptions<SeedAdminOptions> options,
        ILogger<SeedAdminHostedService> logger)
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            _logger.LogInformation("Seed admin is disabled; skipping admin bootstrap.");
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var userService = scope.ServiceProvider.GetRequiredService<UserService>();

        var existingAdmin = await userService.GetByUserNameAsync(_options.UserName, cancellationToken);
        if (existingAdmin is not null)
        {
            return;
        }

        await userService.CreateAsync(new AppUser
        {
            UserName = _options.UserName,
            DisplayName = _options.DisplayName,
            Email = _options.Email,
            Role = SystemRoles.Admin,
            IsActive = true
        }, _options.Password, cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}