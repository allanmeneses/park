using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Security;
using Testcontainers.PostgreSql;
using Xunit;

namespace Parking.Tests.E2E.Infrastructure;

/// <summary>SPEC §23 — Postgres 16 + API; SPEC §24 área <c>Infrastructure</c>.</summary>
[CollectionDefinition(nameof(PostgresApiCollection))]
public sealed class PostgresApiCollection : ICollectionFixture<PostgresWebAppFixture>;

/// <summary>Postgres + API para integração (SPEC §23). Um container por coleção.</summary>
public sealed class PostgresWebAppFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _pg;

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _pg = new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("postgres")
            .WithUsername("parking")
            .WithPassword("parking_dev")
            .WithCommand("-c", "max_connections=400")
            .Build();
        await _pg.StartAsync();

        var csb = new NpgsqlConnectionStringBuilder(_pg.GetConnectionString());
        var adminCs = new NpgsqlConnectionStringBuilder(csb.ConnectionString) { Database = "postgres" }.ConnectionString;
        await using (var conn = new NpgsqlConnection(adminCs))
        {
            await conn.OpenAsync();
            foreach (var db in new[] { "parking_identity", "parking_audit" })
            {
                await using var cmd = new NpgsqlCommand($"CREATE DATABASE \"{db}\"", conn);
                try
                {
                    await cmd.ExecuteNonQueryAsync();
                }
                catch (PostgresException ex) when (ex.SqlState == "42P04")
                {
                }
            }
        }

        var identityCs = new NpgsqlConnectionStringBuilder(csb.ConnectionString) { Database = "parking_identity" }.ConnectionString;
        var auditCs = new NpgsqlConnectionStringBuilder(csb.ConnectionString) { Database = "parking_audit" }.ConnectionString;
        var templateCs = $"Host={csb.Host};Port={csb.Port};Database=parking_{{uuid}};Username={csb.Username};Password={csb.Password}";

        Environment.SetEnvironmentVariable("DATABASE_URL_IDENTITY", identityCs);
        Environment.SetEnvironmentVariable("DATABASE_URL_AUDIT", auditCs);
        Environment.SetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE", templateCs);
        Environment.SetEnvironmentVariable("JWT_SECRET", new string('a', 32));
        Environment.SetEnvironmentVariable("PIX_WEBHOOK_SECRET", new string('b', 32));
        Environment.SetEnvironmentVariable("PIX_MODE", "Stub");
        Environment.SetEnvironmentVariable(
            "TENANT_SECRET_ENCRYPTION_KEY",
            Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(new string('k', 32))));
        Environment.SetEnvironmentVariable("E2E_SEED", "1");

        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(w =>
        {
            w.UseEnvironment("Development");
            w.ConfigureServices(services => { services.RemoveAll(typeof(IHostedService)); });
        });

        await SeedSuperAdminAsync(identityCs);
    }

    private static async Task SeedSuperAdminAsync(string identityCs)
    {
        var opts = new DbContextOptionsBuilder<IdentityDbContext>();
        Parking.Infrastructure.Persistence.NpgsqlEnumConfigurator.ConfigureIdentityNpgsql(opts, identityCs);
        await using var db = new IdentityDbContext(opts.Options);
        await db.Database.MigrateAsync();
        if (await db.Users.AnyAsync(u => u.Email == "super@test.com"))
            return;
        db.Users.Add(new ParkingIdentityUser
        {
            Id = Guid.NewGuid(),
            Email = "super@test.com",
            PasswordHash = Argon2PasswordHasher.Hash("Super!12345"),
            Role = UserRole.SUPER_ADMIN,
            ParkingId = null,
            EntityId = null,
            Active = true,
            OperatorSuspended = false,
            CreatedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        Environment.SetEnvironmentVariable("DATABASE_URL_IDENTITY", null);
        Environment.SetEnvironmentVariable("DATABASE_URL_AUDIT", null);
        Environment.SetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE", null);
        Environment.SetEnvironmentVariable("JWT_SECRET", null);
        Environment.SetEnvironmentVariable("PIX_WEBHOOK_SECRET", null);
        Environment.SetEnvironmentVariable("PIX_MODE", null);
        Environment.SetEnvironmentVariable("TENANT_SECRET_ENCRYPTION_KEY", null);
        Environment.SetEnvironmentVariable("E2E_SEED", null);
        if (Factory != null)
            await Factory.DisposeAsync();
        if (_pg != null)
            await _pg.DisposeAsync();
    }
}
