using Microsoft.EntityFrameworkCore;
using Npgsql;
using Npgsql.NameTranslation;
using Parking.Domain;
using Parking.Infrastructure.Persistence.Audit;
using Parking.Infrastructure.Persistence.Identity;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Infrastructure.Persistence;

public static class NpgsqlEnumConfigurator
{
    private static readonly INpgsqlNameTranslator EnumNames = new NpgsqlNullNameTranslator();

    public static void ConfigureTenantNpgsql(DbContextOptionsBuilder<TenantDbContext> options, string connectionString)
    {
        options.UseNpgsql(connectionString, o =>
        {
            o.MapEnum<TicketStatus>("ticket_status", "public", EnumNames);
            o.MapEnum<PaymentStatus>("payment_status", "public", EnumNames);
            o.MapEnum<PaymentMethod>("payment_method", "public", EnumNames);
            o.MapEnum<CashSessionStatus>("cash_session_status", "public", EnumNames);
        }).UseSnakeCaseNamingConvention();
    }

    public static void ConfigureIdentityNpgsql(DbContextOptionsBuilder<IdentityDbContext> options, string connectionString)
    {
        options.UseNpgsql(connectionString, o => o.MapEnum<UserRole>("user_role", "public", EnumNames))
            .UseSnakeCaseNamingConvention();
    }

    public static void ConfigureAuditNpgsql(DbContextOptionsBuilder<AuditDbContext> options, string connectionString)
    {
        options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention();
    }
}
