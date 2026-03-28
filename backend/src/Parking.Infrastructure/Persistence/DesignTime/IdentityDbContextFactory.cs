using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Parking.Infrastructure.Persistence.Identity;

namespace Parking.Infrastructure.Persistence.DesignTime;

public sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    public IdentityDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("DATABASE_URL_IDENTITY")
                 ?? "Host=localhost;Port=5432;Database=parking_identity;Username=parking;Password=parking_dev";
        var b = new DbContextOptionsBuilder<IdentityDbContext>();
        NpgsqlEnumConfigurator.ConfigureIdentityNpgsql(b, cs);
        return new IdentityDbContext(b.Options);
    }
}
