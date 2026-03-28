using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Parking.Infrastructure.Persistence;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Infrastructure.Tenants;

public sealed class TenantProvisioner(ITenantDbContextFactory tenantFactory)
{
    public async Task CreateAndMigrateTenantAsync(string adminConnectionString, Guid parkingId, CancellationToken ct)
    {
        var dbName = $"parking_{parkingId:N}";
        await using (var conn = new NpgsqlConnection(adminConnectionString))
        {
            await conn.OpenAsync(ct);
            await using var cmd = new NpgsqlCommand(
                $"CREATE DATABASE \"{dbName}\" WITH TEMPLATE template0 ENCODING 'UTF8'",
                conn);
            try
            {
                await cmd.ExecuteNonQueryAsync(ct);
            }
            catch (PostgresException ex) when (ex.SqlState == "42P04")
            {
                // already exists
            }
        }

        var template = adminConnectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase)
            ? Regex.Replace(
                adminConnectionString, "Database=[^;]+", $"Database={dbName}", RegexOptions.IgnoreCase)
            : throw new InvalidOperationException("Invalid admin connection string for tenant.");

        await using (var tctx = tenantFactory.CreateReadWrite(template))
        {
            await tctx.Database.MigrateAsync(ct);
            var settingsId = Guid.Parse("00000000-0000-0000-0000-000000000000");
            if (!await tctx.Settings.AnyAsync(s => s.Id == settingsId, ct))
            {
                tctx.Settings.Add(new SettingsRow
                {
                    Id = settingsId,
                    PricePerHour = 5.00m,
                    Capacity = 50,
                });
                await tctx.SaveChangesAsync(ct);
            }
        }
    }
}
