using LifeLine.Application.Interfaces;
using LifeLine.Application.Interfaces.IServices;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LifeLine.Persistence.Services;

public class EmailBackgroundService : BackgroundService
{
    private readonly IEmailBackgroundQueue _queue;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<EmailBackgroundService> _logger;

    public EmailBackgroundService(
        IEmailBackgroundQueue queue,
        IServiceScopeFactory scopeFactory,
        ILogger<EmailBackgroundService> logger)
    {
        _queue = queue;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(
        CancellationToken stoppingToken)
    {
        _logger.LogInformation("LifeLine email background service started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var (email, name) =
                    await _queue.DequeueAsync(stoppingToken);

                using var scope = _scopeFactory.CreateScope();

                var emailService =
                    scope.ServiceProvider.GetRequiredService<IEmailService>();

                await emailService.SendWelcomeEmailAsync(email, name);

                _logger.LogInformation(
                    "Welcome email sent successfully to {Email}",
                    email);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to send background email.");
            }
        }

        _logger.LogInformation("LifeLine email background service stopped.");
    }
}