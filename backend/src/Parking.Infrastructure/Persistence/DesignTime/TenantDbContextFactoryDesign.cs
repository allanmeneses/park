using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Infrastructure.Persistence.DesignTime;

public sealed class TenantDbContextFactoryDesign : IDesignTimeDbContextFactory<TenantDbContext>
{
    public TenantDbContext CreateDbContext(string[] args)
    {
        var template = Environment.GetEnvironmentVariable("TENANT_DATABASE_URL_TEMPLATE")
                       ?? "Host=localhost;Port=5432;Database=parking_{uuid};Username=parking;Password=parking_dev";
        var sampleTenant = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var cs = template.Replace("{uuid}", sampleTenant.ToString("N"), StringComparison.Ordinal);
        var b = new DbContextOptionsBuilder<TenantDbContext>();
        NpgsqlEnumConfigurator.ConfigureTenantNpgsql(b, cs);
        return new TenantDbContext(b.Options);
    }
}
