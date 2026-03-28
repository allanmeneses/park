using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Parking.Infrastructure.Persistence.Identity;

namespace Parking.Api.Hosting;

/// <summary>SPEC §12 — intervalo configurável (default 60s).</summary>
public sealed class PixExpiryBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<PixExpiryBackgroundService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sec = 60;
        if (int.TryParse(configuration["PIX_EXPIRY_JOB_SECONDS"], out var p) && p > 0)
            sec = p;
        var delay = TimeSpan.FromSeconds(sec);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                var runner = scope.ServiceProvider.GetRequiredService<PixExpiryRunner>();
                await runner.RunForAllTenantsAsync(identity, stoppingToken);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Falha no job de expiração PIX.");
            }

            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
