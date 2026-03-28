using Microsoft.EntityFrameworkCore;
using Parking.Infrastructure.Persistence;
using Parking.Infrastructure.Persistence.Tenant;

namespace Parking.Infrastructure.Tenants;

public interface ITenantDbContextFactory
{
    TenantDbContext CreateReadWrite(string tenantConnectionString);
}

public sealed class TenantDbContextFactory : ITenantDbContextFactory
{
    public TenantDbContext CreateReadWrite(string tenantConnectionString)
    {
        var b = new DbContextOptionsBuilder<TenantDbContext>();
        NpgsqlEnumConfigurator.ConfigureTenantNpgsql(b, tenantConnectionString);
        return new TenantDbContext(b.Options);
    }
}
