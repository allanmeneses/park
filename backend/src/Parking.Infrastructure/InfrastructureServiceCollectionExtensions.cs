using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Parking.Infrastructure.Auth;
using Parking.Infrastructure.Persistence;
using Parking.Infrastructure.Persistence.Audit;
using Parking.Infrastructure.Audit;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Pix;
using Parking.Infrastructure.Tenants;

namespace Parking.Infrastructure;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddParkingInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<JwtOptions>(o =>
        {
            configuration.GetSection(JwtOptions.SectionName).Bind(o);
            if (string.IsNullOrEmpty(o.Secret))
                o.Secret = configuration["JWT_SECRET"] ?? "";
        });

        var identityCs = configuration["DATABASE_URL_IDENTITY"]
                         ?? throw new InvalidOperationException("DATABASE_URL_IDENTITY is required");
        var auditCs = configuration["DATABASE_URL_AUDIT"]
                      ?? throw new InvalidOperationException("DATABASE_URL_AUDIT is required");

        services.AddScoped<IdentityDbContext>(_ =>
        {
            var b = new DbContextOptionsBuilder<IdentityDbContext>();
            NpgsqlEnumConfigurator.ConfigureIdentityNpgsql(b, identityCs);
            return new IdentityDbContext(b.Options);
        });
        services.AddScoped<AuditDbContext>(_ =>
        {
            var b = new DbContextOptionsBuilder<AuditDbContext>();
            NpgsqlEnumConfigurator.ConfigureAuditNpgsql(b, auditCs);
            return new AuditDbContext(b.Options);
        });

        services.AddSingleton<ITenantDbContextFactory, TenantDbContextFactory>();
        services.AddScoped<IOperatorProblemAuthCheck, OperatorProblemAuthCheck>();
        services.AddSingleton<JwtTokenService>();
        services.AddScoped<AuditService>();
        services.AddScoped<TenantProvisioner>();

        var pixMode = configuration["PIX_MODE"] ?? "Stub";
        if (string.Equals(pixMode, "Production", StringComparison.OrdinalIgnoreCase))
            services.AddSingleton<IPixPaymentAdapter, ProductionPixProvider>();
        else
            services.AddSingleton<IPixPaymentAdapter, StubPixProvider>();

        return services;
    }
}
