using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Parking.Infrastructure.Persistence.Audit;

namespace Parking.Infrastructure.Persistence.DesignTime;

public sealed class AuditDbContextFactory : IDesignTimeDbContextFactory<AuditDbContext>
{
    public AuditDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("DATABASE_URL_AUDIT")
                 ?? "Host=localhost;Port=5432;Database=parking_audit;Username=parking;Password=parking_dev";
        var b = new DbContextOptionsBuilder<AuditDbContext>();
        NpgsqlEnumConfigurator.ConfigureAuditNpgsql(b, cs);
        return new AuditDbContext(b.Options);
    }
}
