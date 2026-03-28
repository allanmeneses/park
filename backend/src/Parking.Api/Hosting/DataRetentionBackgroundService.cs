using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Parking.Infrastructure.Persistence.Identity;

namespace Parking.Api.Hosting;

/// <summary>SPEC — retenção idempotency (24h), webhook_receipts (30d), audit (365d).</summary>
public sealed class DataRetentionBackgroundService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DataRetentionBackgroundService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var sec = 3600;
        if (int.TryParse(configuration["DATA_RETENTION_JOB_SECONDS"], out var p) && p >= 60)
            sec = p;
        var delay = TimeSpan.FromSeconds(sec);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var identity = scope.ServiceProvider.GetRequiredService<IdentityDbContext>();
                var data = scope.ServiceProvider.GetRequiredService<DataRetentionRunner>();
                var audit = scope.ServiceProvider.GetRequiredService<AuditRetentionRunner>();
                await data.RunForAllTenantsAsync(identity, stoppingToken);
                await audit.PurgeOlderThan365DaysAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Falha no job de retenção de dados.");
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
